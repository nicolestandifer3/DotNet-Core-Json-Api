using System.Collections.Generic;
using System.Net;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Models.JsonApiDocuments;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace UnitTests
{
    public sealed class JsonApiControllerMixin_Tests : JsonApiControllerMixin
    {

        [Fact]
        public void Errors_Correctly_Infers_Status_Code()
        {
            // Arrange
            var errors422 = new List<Error>
            {
                new Error(HttpStatusCode.UnprocessableEntity, "bad specific"),
                new Error(HttpStatusCode.UnprocessableEntity, "bad other specific")
            };

            var errors400 = new List<Error>
            {
                new Error(HttpStatusCode.OK, "weird"),
                new Error(HttpStatusCode.BadRequest, "bad"),
                new Error(HttpStatusCode.UnprocessableEntity, "bad specific"),
            };

            var errors500 = new List<Error>
            {
                new Error(HttpStatusCode.OK, "weird"),
                new Error(HttpStatusCode.BadRequest, "bad"),
                new Error(HttpStatusCode.UnprocessableEntity, "bad specific"),
                new Error(HttpStatusCode.InternalServerError, "really bad"),
                new Error(HttpStatusCode.BadGateway, "really bad specific"),
            };
            
            // Act
            var result422 = Errors(errors422);
            var result400 = Errors(errors400);
            var result500 = Errors(errors500);
            
            // Assert
            var response422 = Assert.IsType<ObjectResult>(result422);
            var response400 = Assert.IsType<ObjectResult>(result400);
            var response500 = Assert.IsType<ObjectResult>(result500);

            Assert.Equal((int)HttpStatusCode.UnprocessableEntity, response422.StatusCode);
            Assert.Equal((int)HttpStatusCode.BadRequest, response400.StatusCode);
            Assert.Equal((int)HttpStatusCode.InternalServerError, response500.StatusCode);
        }
    }
}
