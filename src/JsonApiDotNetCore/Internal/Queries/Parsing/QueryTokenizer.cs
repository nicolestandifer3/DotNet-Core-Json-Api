using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace JsonApiDotNetCore.Internal.Queries.Parsing
{
    public sealed class QueryTokenizer
    {
        public static readonly IReadOnlyDictionary<char, TokenKind> SingleCharacterToTokenKinds =
            new ReadOnlyDictionary<char, TokenKind>(new Dictionary<char, TokenKind>
            {
                ['('] = TokenKind.OpenParen,
                [')'] = TokenKind.CloseParen,
                ['['] = TokenKind.OpenBracket,
                [']'] = TokenKind.CloseBracket,
                [','] = TokenKind.Comma,
                [':'] = TokenKind.Colon,
                ['-'] = TokenKind.Minus
            });

        private readonly string _source;
        private readonly StringBuilder _textBuffer = new StringBuilder();
        private int _offset;
        private bool _isInQuotedSection;

        public QueryTokenizer(string source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public IEnumerable<Token> EnumerateTokens()
        {
            _textBuffer.Clear();
            _isInQuotedSection = false;
            _offset = 0;

            while (_offset < _source.Length)
            {
                char ch = _source[_offset];

                if (ch == '\'')
                {
                    if (_isInQuotedSection)
                    {
                        char? peeked = PeekChar();

                        if (peeked == '\'')
                        {
                            _textBuffer.Append(ch);
                            _offset += 2;
                            continue;
                        }

                        _isInQuotedSection = false;

                        Token literalToken = FlushTextBuffer(true);
                        yield return literalToken;
                    }
                    else
                    {
                        if (_textBuffer.Length > 0)
                        {
                            throw new QueryParseException("Unexpected ' outside text.");
                        }

                        _isInQuotedSection = true;
                    }
                }
                else
                {
                    TokenKind? singleCharacterTokenKind = _isInQuotedSection ? null : TryGetSingleCharacterTokenKind(ch);

                    if (singleCharacterTokenKind != null && !IsMinusInsideText(singleCharacterTokenKind.Value))
                    {
                        Token identifierToken = FlushTextBuffer(false);

                        if (identifierToken != null)
                        {
                            yield return identifierToken;
                        }

                        yield return new Token(singleCharacterTokenKind.Value);
                    }
                    else
                    {
                        if (_textBuffer.Length == 0 && ch == ' ')
                        {
                            throw new QueryParseException("Unexpected whitespace.");
                        }

                        _textBuffer.Append(ch);
                    }
                }

                _offset++;
            }

            if (_isInQuotedSection)
            {
                throw new QueryParseException("' expected.");
            }

            Token lastToken = FlushTextBuffer(false);

            if (lastToken != null)
            {
                yield return lastToken;
            }
        }

        private bool IsMinusInsideText(TokenKind kind)
        {
            return kind == TokenKind.Minus && _textBuffer.Length > 0;
        }

        private char? PeekChar()
        {
            return _offset + 1 < _source.Length ? (char?)_source[_offset + 1] : null;
        }

        private static TokenKind? TryGetSingleCharacterTokenKind(char ch)
        {
            return SingleCharacterToTokenKinds.ContainsKey(ch) ? (TokenKind?)SingleCharacterToTokenKinds[ch] : null;
        }

        private Token FlushTextBuffer(bool isQuotedText)
        {
            if (isQuotedText || _textBuffer.Length > 0)
            {
                string text = _textBuffer.ToString();
                _textBuffer.Clear();
                return new Token(isQuotedText ? TokenKind.QuotedText : TokenKind.Text, text);
            }

            return null;
        }
    }
}
