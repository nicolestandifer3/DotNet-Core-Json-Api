using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCoreExampleTests.Startups;
using Microsoft.Extensions.DependencyInjection;
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.RequiredRelationships
{
    public sealed class DefaultBehaviorTests : IClassFixture<ExampleIntegrationTestContext<TestableStartup<DefaultBehaviorDbContext>, DefaultBehaviorDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<DefaultBehaviorDbContext>, DefaultBehaviorDbContext> _testContext;

        private readonly DefaultBehaviorFakers _fakers = new DefaultBehaviorFakers();

        public DefaultBehaviorTests(ExampleIntegrationTestContext<TestableStartup<DefaultBehaviorDbContext>, DefaultBehaviorDbContext> testContext)
        {
            _testContext = testContext;

            var options = (JsonApiOptions) testContext.Factory.Services.GetRequiredService<IJsonApiOptions>();
            options.UseRelativeLinks = true;
        }

        [Fact]
        public async Task Cannot_create_dependent_side_of_required_ManyToOne_relationship_without_providing_principal_side()
        {
            // Arrange
            var order = _fakers.Orders.Generate();

            var requestBody = new
            {
                data = new
                {
                    type = "orders",
                    attributes = new
                    {
                        order = order.Amount
                    }
                }
            };

            const string route = "/orders";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePostAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.InternalServerError);

            responseDocument.Errors.Should().HaveCount(1);

            var error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
            error.Title.Should().Be("An unhandled error occurred while processing this request.");
            error.Detail.Should().Be("Failed to persist changes in the underlying data store.");
        }

        [Fact]
        public async Task Cannot_create_dependent_side_of_required_OneToOne_relationship_without_providing_principal_side()
        {
            // Arrange
            var shipment = _fakers.Shipments.Generate();

            var requestBody = new
            {
                data = new
                {
                    type = "shipments",
                    attributes = new
                    {
                        trackAndTraceCode = shipment.TrackAndTraceCode
                    }
                }
            };

            const string route = "/shipments";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePostAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.InternalServerError);

            responseDocument.Errors.Should().HaveCount(1);

            var error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
            error.Title.Should().Be("An unhandled error occurred while processing this request.");
            error.Detail.Should().Be("Failed to persist changes in the underlying data store.");
        }

        [Fact]
        public async Task Deleting_principal_side_of_required_OneToMany_relationship_triggers_cascading_delete()
        {
            // Arrange
            var existingOrder = _fakers.Orders.Generate();
            existingOrder.Customer = _fakers.Customers.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Orders.Add(existingOrder);
                await dbContext.SaveChangesAsync();
            });

            var route = $"/customers/{existingOrder.Customer.Id}";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecuteDeleteAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeNull();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                var existingCustomerInDatabase = await dbContext.Customers.FirstWithIdOrDefaultAsync(existingOrder.Customer.Id);
                existingCustomerInDatabase.Should().BeNull();

                var existingOrderInDatabase = await dbContext.Orders.FirstWithIdOrDefaultAsync(existingOrder.Id);
                existingOrderInDatabase.Should().BeNull();
            });
        }

        [Fact]
        public async Task Deleting_principal_side_of_required_OneToOne_relationship_triggers_cascading_delete()
        {
            // Arrange
            var existingOrder = _fakers.Orders.Generate();
            existingOrder.Shipment = _fakers.Shipments.Generate();
            existingOrder.Customer = _fakers.Customers.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Orders.Add(existingOrder);
                await dbContext.SaveChangesAsync();
            });

            var route = $"/orders/{existingOrder.Id}";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecuteDeleteAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeNull();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                var existingOrderInDatabase = await dbContext.Orders.FirstWithIdOrDefaultAsync(existingOrder.Id);
                existingOrderInDatabase.Should().BeNull();

                var existingShipmentInDatabase = await dbContext.Shipments.FirstWithIdOrDefaultAsync(existingOrder.Shipment.Id);
                existingShipmentInDatabase.Should().BeNull();

                var existingCustomerInDatabase = await dbContext.Customers.FirstWithIdOrDefaultAsync(existingOrder.Customer.Id);
                existingCustomerInDatabase.Should().NotBeNull();
            });
        }

        [Fact]
        public async Task Cannot_clear_required_ManyToOne_relationship_through_primary_endpoint()
        {
            // Arrange
            var existingOrder = _fakers.Orders.Generate();
            existingOrder.Shipment = _fakers.Shipments.Generate();
            existingOrder.Customer = _fakers.Customers.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Orders.Add(existingOrder);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    id = existingOrder.Id,
                    type = "orders",
                    relationships = new
                    {
                        customer = new
                        {
                            data = (object) null
                        }
                    }
                }
            };

            var route = $"/orders/{existingOrder.Id}";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePatchAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.BadRequest);

            responseDocument.Errors.Should().HaveCount(1);

            var error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            error.Title.Should().Be("Failed to clear a required relationship.");
            error.Detail.Should().Be($"The relationship 'customer' of resource type 'orders' with ID '{existingOrder.StringId}' " +
                "cannot be cleared because it is a required relationship.");
        }

        [Fact]
        public async Task Cannot_clear_required_ManyToOne_relationship_through_relationship_endpoint()
        {
            // Arrange
            var existingOrder = _fakers.Orders.Generate();
            existingOrder.Shipment = _fakers.Shipments.Generate();
            existingOrder.Customer = _fakers.Customers.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Orders.Add(existingOrder);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = (object) null
            };

            var route = $"/orders/{existingOrder.Id}/relationships/customer";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePatchAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.BadRequest);

            responseDocument.Errors.Should().HaveCount(1);

            var error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            error.Title.Should().Be("Failed to clear a required relationship.");
            error.Detail.Should().Be($"The relationship 'customer' of resource type 'orders' with ID '{existingOrder.StringId}' " +
                "cannot be cleared because it is a required relationship.");
        }

        [Fact]
        public async Task Cannot_clear_required_OneToMany_relationship_through_primary_endpoint()
        {
            // Arrange
            var existingOrder = _fakers.Orders.Generate();
            existingOrder.Shipment = _fakers.Shipments.Generate();
            existingOrder.Customer = _fakers.Customers.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Orders.Add(existingOrder);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    id = existingOrder.Customer.Id,
                    type = "customers",
                    relationships = new
                    {
                        orders = new
                        {
                            data = new object[0]
                        }
                    }
                }
            };

            var route = $"/customers/{existingOrder.Customer.Id}";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePatchAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.BadRequest);

            responseDocument.Errors.Should().HaveCount(1);

            var error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            error.Title.Should().Be("Failed to clear a required relationship.");
            error.Detail.Should().Be($"The relationship 'orders' of resource type 'customers' with ID '{existingOrder.StringId}' " +
                "cannot be cleared because it is a required relationship.");
        }

        [Fact]
        public async Task Cannot_clear_required_OneToMany_relationship_by_updating_through_relationship_endpoint()
        {
            // Arrange
            var existingOrder = _fakers.Orders.Generate();
            existingOrder.Shipment = _fakers.Shipments.Generate();
            existingOrder.Customer = _fakers.Customers.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Orders.Add(existingOrder);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new object[0]
            };

            var route = $"/customers/{existingOrder.Customer.Id}/relationships/orders";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePatchAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.BadRequest);

            responseDocument.Errors.Should().HaveCount(1);

            var error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            error.Title.Should().Be("Failed to clear a required relationship.");
            error.Detail.Should().Be($"The relationship 'orders' of resource type 'customers' with ID '{existingOrder.StringId}' " +
                "cannot be cleared because it is a required relationship.");
        }

        [Fact]
        public async Task Cannot_clear_required_OneToMany_relationship_by_deleting_through_relationship_endpoint()
        {
            // Arrange
            var existingOrder = _fakers.Orders.Generate();
            existingOrder.Shipment = _fakers.Shipments.Generate();
            existingOrder.Customer = _fakers.Customers.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Orders.Add(existingOrder);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new object[]
                {
                    new
                    {
                       type = "orders",
                       id = existingOrder.Id
                    }
                }
            };

            var route = $"/customers/{existingOrder.Customer.Id}/relationships/orders";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecuteDeleteAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.BadRequest);

            responseDocument.Errors.Should().HaveCount(1);

            var error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            error.Title.Should().Be("Failed to clear a required relationship.");
            error.Detail.Should().Be($"The relationship 'orders' of resource type 'customers' with ID '{existingOrder.StringId}' " +
                "cannot be cleared because it is a required relationship.");
        }

        [Fact]
        public async Task Cannot_reassign_dependent_side_of_OneToOne_relationship_with_identifying_foreign_key_through_primary_endpoint()
        {
            // Arrange
            var orderWithShipment = _fakers.Orders.Generate();
            orderWithShipment.Shipment = _fakers.Shipments.Generate();
            orderWithShipment.Customer = _fakers.Customers.Generate();

            var orderWithoutShipment = _fakers.Orders.Generate();
            orderWithoutShipment.Customer = _fakers.Customers.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Orders.AddRange(orderWithShipment, orderWithoutShipment);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    id = orderWithoutShipment.Id,
                    type = "orders",
                    relationships = new
                    {
                        shipment = new
                        {
                            data = new
                            {
                                id = orderWithShipment.Shipment.Id,
                                type = "shipments"
                            }
                        }
                    }
                }
            };

            var route = $"/orders/{orderWithoutShipment.Id}";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePatchAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.InternalServerError);

            responseDocument.Errors.Should().HaveCount(1);

            var error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
            error.Title.Should().Be("An unhandled error occurred while processing this request.");
            error.Detail.Should().StartWith("The property 'Id' on entity type 'Shipment' is part of a key and so cannot be modified or marked as modified.");
        }

        [Fact]
        public async Task Cannot_reassign_dependent_side_of_OneToOne_relationship_with_identifying_foreign_key_through_relationship_endpoint()
        {
            // Arrange
            var orderWithShipment = _fakers.Orders.Generate();
            orderWithShipment.Shipment = _fakers.Shipments.Generate();
            orderWithShipment.Customer = _fakers.Customers.Generate();

            var orderWithoutShipment = _fakers.Orders.Generate();
            orderWithoutShipment.Customer = _fakers.Customers.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Orders.AddRange(orderWithShipment, orderWithoutShipment);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    id = orderWithShipment.Shipment.Id,
                    type = "shipments"
                }
            };

            var route = $"/orders/{orderWithoutShipment.Id}/relationships/shipment";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePatchAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.InternalServerError);

            responseDocument.Errors.Should().HaveCount(1);

            var error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
            error.Title.Should().Be("An unhandled error occurred while processing this request.");
            error.Detail.Should().StartWith("The property 'Id' on entity type 'Shipment' is part of a key and so cannot be modified or marked as modified.");
        }
    }
}
