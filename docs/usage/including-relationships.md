# Including Relationships

JADNC supports [request include params](http://jsonapi.org/format/#fetching-includes) out of the box, 
for side loading related resources.

```http
GET /articles/1?include=comments HTTP/1.1
Accept: application/vnd.api+json

{
  "data": {
    "type": "articles",
    "id": "1",
    "attributes": {
      "title": "JSON API paints my bikeshed!"
    },
    "relationships": {
      "comments": {
        "links": {
          "self": "http://example.com/articles/1/relationships/comments",
          "related": "http://example.com/articles/1/comments"
        },
        "data": [
          { "type": "comments", "id": "5" },
          { "type": "comments", "id": "12" }
        ]
      }
    }
  },
  "included": [
    {
      "type": "comments",
      "id": "5",
      "attributes": {
        "body": "First!"
      }
    }, 
    {
      "type": "comments",
      "id": "12",
      "attributes": {
        "body": "I like XML better"
      }
    }
  ]
}
```

## Deeply Nested Inclusions

_since v3.0.0_

JsonApiDotNetCore also supports deeply nested inclusions. 
This allows you to include data across relationships by using a period delimited
relationship path such as comments.author.