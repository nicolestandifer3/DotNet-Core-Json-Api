﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Models.Operations;
using JsonApiDotNetCore.Services.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace UnitTests.Services
{
    public class OperationsProcessorTests
    {
        private readonly Mock<IOperationProcessorResolver> _resolverMock;
        public readonly Mock<DbContext> _dbContextMock;
        public readonly Mock<IDbContextResolver> _dbContextResolverMock;

        public OperationsProcessorTests()
        {
            _resolverMock = new Mock<IOperationProcessorResolver>();
            _dbContextMock = new Mock<DbContext>();
            _dbContextResolverMock = new Mock<IDbContextResolver>();
        }

        [Fact]
        public async Task ProcessAsync_Performs_Pointer_ReplacementAsync()
        {
            // arrange
            var request = @"[
                {
                    ""op"": ""add"",
                    ""data"": {
                        ""type"": ""authors"",
                        ""lid"": ""a"",
                        ""attributes"": {
                            ""name"": ""dgeb""
                        }
                    }
                }, {
                    ""op"": ""add"",
                    ""data"": {
                        ""type"": ""articles"",
                        ""attributes"": {
                            ""title"": ""JSON API paints my bikeshed!""
                        },
                        ""relationships"": {
                            ""author"": {
                                ""data"": {
                                    ""type"": ""authors"",
                                    ""lid"": ""a""
                                }
                            }
                        }
                    }
                }
            ]";

            var op1Result = @"{
                ""links"": {
                    ""self"": ""http://example.com/authors/9""
                },
                ""data"": {
                    ""type"": ""authors"",
                    ""id"": ""9"",
                    ""lid"": ""a"",
                    ""attributes"": {
                        ""name"": ""dgeb""
                    }
                }
            }";

            var operations = JsonConvert.DeserializeObject<List<Operation>>(request);
            var addOperationResult = JsonConvert.DeserializeObject<Operation>(op1Result);

            var databaseMock = new Mock<DatabaseFacade>(_dbContextMock.Object);
            var transactionMock = new Mock<IDbContextTransaction>();
            databaseMock.Setup(m => m.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(transactionMock.Object);
            _dbContextMock.Setup(m => m.Database).Returns(databaseMock.Object);

            var opProcessorMock = new Mock<IOpProcessor>();
            opProcessorMock.Setup(m => m.ProcessAsync(It.Is<Operation>(op => op.DataObject.Type.ToString() == "authors")))
                .ReturnsAsync(addOperationResult);

            _resolverMock.Setup(m => m.LocateCreateService(It.IsAny<Operation>()))
                .Returns(opProcessorMock.Object);

            _resolverMock.Setup(m => m.LocateCreateService((It.IsAny<Operation>())))
                .Returns(opProcessorMock.Object);

            _dbContextResolverMock.Setup(m => m.GetContext()).Returns(_dbContextMock.Object);
            var operationsProcessor = new OperationsProcessor(_resolverMock.Object, _dbContextResolverMock.Object);

            // act
            var results = await operationsProcessor.ProcessAsync(operations);

            // assert
            opProcessorMock.Verify(
                m => m.ProcessAsync(
                    It.Is<Operation>(o =>
                        o.DataObject.Type.ToString() == "articles"
                        && o.DataObject.Relationships["author"].SingleData.Id == "9"
                    )
                )
            );
        }
    }
}
