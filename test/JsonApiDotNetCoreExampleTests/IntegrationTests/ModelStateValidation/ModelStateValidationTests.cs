using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Serialization.Objects;
using Newtonsoft.Json;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ModelStateValidation
{
    public sealed class ModelStateValidationTests : IClassFixture<IntegrationTestContext<ModelStateValidationStartup<ModelStateDbContext>, ModelStateDbContext>>
    {
        private readonly IntegrationTestContext<ModelStateValidationStartup<ModelStateDbContext>, ModelStateDbContext> _testContext;

        public ModelStateValidationTests(IntegrationTestContext<ModelStateValidationStartup<ModelStateDbContext>, ModelStateDbContext> testContext)
        {
            _testContext = testContext;
        }

        [Fact]
        public async Task When_posting_resource_with_omitted_required_attribute_value_it_must_fail()
        {
            // Arrange
            var content = new
            {
                data = new
                {
                    type = "systemDirectories",
                    attributes = new Dictionary<string, object>
                    {
                        ["isCaseSensitive"] = "true"
                    }
                }
            };

            string requestBody = JsonConvert.SerializeObject(content);
            string route = "/systemDirectories";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePostAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(1);
            responseDocument.Errors[0].StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            responseDocument.Errors[0].Title.Should().Be("Input validation failed.");
            responseDocument.Errors[0].Detail.Should().Be("The Name field is required.");
            responseDocument.Errors[0].Source.Pointer.Should().Be("/data/attributes/name");
        }

        [Fact]
        public async Task When_posting_resource_with_null_for_required_attribute_value_it_must_fail()
        {
            // Arrange
            var content = new
            {
                data = new
                {
                    type = "systemDirectories",
                    attributes = new Dictionary<string, object>
                    {
                        ["name"] = null,
                        ["isCaseSensitive"] = "true"
                    }
                }
            };

            string requestBody = JsonConvert.SerializeObject(content);
            string route = "/systemDirectories";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePostAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(1);
            responseDocument.Errors[0].StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            responseDocument.Errors[0].Title.Should().Be("Input validation failed.");
            responseDocument.Errors[0].Detail.Should().Be("The Name field is required.");
            responseDocument.Errors[0].Source.Pointer.Should().Be("/data/attributes/name");
        }

        [Fact]
        public async Task When_posting_resource_with_invalid_attribute_value_it_must_fail()
        {
            // Arrange
            var content = new
            {
                data = new
                {
                    type = "systemDirectories",
                    attributes = new Dictionary<string, object>
                    {
                        ["name"] = "!@#$%^&*().-",
                        ["isCaseSensitive"] = "true"
                    }
                }
            };

            string requestBody = JsonConvert.SerializeObject(content);
            string route = "/systemDirectories";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePostAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(1);
            responseDocument.Errors[0].StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            responseDocument.Errors[0].Title.Should().Be("Input validation failed.");
            responseDocument.Errors[0].Detail.Should().Be("The field Name must match the regular expression '^[\\w\\s]+$'.");
            responseDocument.Errors[0].Source.Pointer.Should().Be("/data/attributes/name");
        }

        [Fact]
        public async Task When_posting_resource_with_valid_attribute_value_it_must_succeed()
        {
            // Arrange
            var content = new
            {
                data = new
                {
                    type = "systemDirectories",
                    attributes = new Dictionary<string, object>
                    {
                        ["name"] = "Projects",
                        ["isCaseSensitive"] = "true"
                    }
                }
            };

            string requestBody = JsonConvert.SerializeObject(content);
            string route = "/systemDirectories";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Attributes["name"].Should().Be("Projects");
            responseDocument.SingleData.Attributes["isCaseSensitive"].Should().Be(true);
        }

        [Fact]
        public async Task When_posting_resource_with_multiple_violations_it_must_fail()
        {
            // Arrange
            var content = new
            {
                data = new
                {
                    type = "systemDirectories",
                    attributes = new Dictionary<string, object>
                    {
                        ["sizeInBytes"] = "-1"
                    }
                }
            };

            string requestBody = JsonConvert.SerializeObject(content);
            string route = "/systemDirectories";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePostAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(3);

            responseDocument.Errors[0].StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            responseDocument.Errors[0].Title.Should().Be("Input validation failed.");
            responseDocument.Errors[0].Detail.Should().Be("The Name field is required.");
            responseDocument.Errors[0].Source.Pointer.Should().Be("/data/attributes/name");

            responseDocument.Errors[1].StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            responseDocument.Errors[1].Title.Should().Be("Input validation failed.");
            responseDocument.Errors[1].Detail.Should().Be("The field SizeInBytes must be between 0 and 9223372036854775807.");
            responseDocument.Errors[1].Source.Pointer.Should().Be("/data/attributes/sizeInBytes");

            responseDocument.Errors[2].StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            responseDocument.Errors[2].Title.Should().Be("Input validation failed.");
            responseDocument.Errors[2].Detail.Should().Be("The IsCaseSensitive field is required.");
            responseDocument.Errors[2].Source.Pointer.Should().Be("/data/attributes/isCaseSensitive");
        }

        [Fact]
        public async Task When_posting_resource_with_annotated_relationships_it_must_succeed()
        {
            // Arrange
            var parentDirectory = new SystemDirectory
            {
                Name = "Shared",
                IsCaseSensitive = true
            };

            var subdirectory = new SystemDirectory
            {
                Name = "Open Source",
                IsCaseSensitive = true
            };

            var file = new SystemFile
            {
                FileName = "Main.cs",
                SizeInBytes = 100
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Directories.AddRange(parentDirectory, subdirectory);
                dbContext.Files.Add(file);

                await dbContext.SaveChangesAsync();
            });

            var content = new
            {
                data = new
                {
                    type = "systemDirectories",
                    attributes = new Dictionary<string, object>
                    {
                        ["name"] = "Projects",
                        ["isCaseSensitive"] = "true"
                    },
                    relationships = new Dictionary<string, object>
                    {
                        ["subdirectories"] = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "systemDirectories",
                                    id = subdirectory.StringId
                                }
                            }
                        },
                        ["files"] = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "systemFiles",
                                    id = file.StringId
                                }
                            }
                        },
                        ["parent"] = new
                        {
                            data = new
                            {
                                type = "systemDirectories",
                                id = parentDirectory.StringId
                            }
                        }
                    }
                }
            };

            string requestBody = JsonConvert.SerializeObject(content);
            string route = "/systemDirectories";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Attributes["name"].Should().Be("Projects");
            responseDocument.SingleData.Attributes["isCaseSensitive"].Should().Be(true);
        }

        [Fact]
        public async Task When_patching_resource_with_omitted_required_attribute_value_it_must_succeed()
        {
            // Arrange
            var directory = new SystemDirectory
            {
                Name = "Projects",
                IsCaseSensitive = true
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Directories.Add(directory);
                await dbContext.SaveChangesAsync();
            });

            var content = new
            {
                data = new
                {
                    type = "systemDirectories",
                    id = directory.StringId,
                    attributes = new Dictionary<string, object>
                    {
                        ["sizeInBytes"] = "100"
                    }
                }
            };

            string requestBody = JsonConvert.SerializeObject(content);
            string route = "/systemDirectories/" + directory.StringId;

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePatchAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.Data.Should().BeNull();
        }

        [Fact]
        public async Task When_patching_resource_with_null_for_required_attribute_value_it_must_fail()
        {
            // Arrange
            var directory = new SystemDirectory
            {
                Name = "Projects",
                IsCaseSensitive = true
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Directories.Add(directory);
                await dbContext.SaveChangesAsync();
            });

            var content = new
            {
                data = new
                {
                    type = "systemDirectories",
                    id = directory.StringId,
                    attributes = new Dictionary<string, object>
                    {
                        ["name"] = null
                    }
                }
            };

            string requestBody = JsonConvert.SerializeObject(content);
            string route = "/systemDirectories/" + directory.StringId;

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePatchAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(1);
            responseDocument.Errors[0].StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            responseDocument.Errors[0].Title.Should().Be("Input validation failed.");
            responseDocument.Errors[0].Detail.Should().Be("The Name field is required.");
            responseDocument.Errors[0].Source.Pointer.Should().Be("/data/attributes/name");
        }

        [Fact]
        public async Task When_patching_resource_with_invalid_attribute_value_it_must_fail()
        {
            // Arrange
            var directory = new SystemDirectory
            {
                Name = "Projects",
                IsCaseSensitive = true
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Directories.Add(directory);
                await dbContext.SaveChangesAsync();
            });

            var content = new
            {
                data = new
                {
                    type = "systemDirectories",
                    id = directory.StringId,
                    attributes = new Dictionary<string, object>
                    {
                        ["name"] = "!@#$%^&*().-"
                    }
                }
            };

            string requestBody = JsonConvert.SerializeObject(content);
            string route = "/systemDirectories/" + directory.StringId;

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePatchAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(1);
            responseDocument.Errors[0].StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            responseDocument.Errors[0].Title.Should().Be("Input validation failed.");
            responseDocument.Errors[0].Detail.Should().Be("The field Name must match the regular expression '^[\\w\\s]+$'.");
            responseDocument.Errors[0].Source.Pointer.Should().Be("/data/attributes/name");
        }

        [Fact]
        public async Task When_patching_resource_with_valid_attribute_value_it_must_succeed()
        {
            // Arrange
            var directory = new SystemDirectory
            {
                Name = "Projects",
                IsCaseSensitive = true
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Directories.Add(directory);
                await dbContext.SaveChangesAsync();
            });

            var content = new
            {
                data = new
                {
                    type = "systemDirectories",
                    id = directory.StringId,
                    attributes = new Dictionary<string, object>
                    {
                        ["name"] = "Repositories"
                    }
                }
            };

            string requestBody = JsonConvert.SerializeObject(content);
            string route = "/systemDirectories/" + directory.StringId;

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePatchAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.Data.Should().BeNull();
        }

        [Fact]
        public async Task When_patching_resource_with_annotated_relationships_it_must_succeed()
        {
            // Arrange
            var directory = new SystemDirectory
            {
                Name = "Projects",
                IsCaseSensitive = false,
                Subdirectories = new List<SystemDirectory>
                {
                    new SystemDirectory
                    {
                        Name = "C#",
                        IsCaseSensitive = false
                    }
                },
                Files = new List<SystemFile>
                {
                    new SystemFile
                    {
                        FileName = "readme.txt"
                    }
                },
                Parent = new SystemDirectory
                {
                    Name = "Data",
                    IsCaseSensitive = false
                }
            };

            var otherParent = new SystemDirectory
            {
                Name = "Shared",
                IsCaseSensitive = false
            };

            var otherSubdirectory = new SystemDirectory
            {
                Name = "Shared",
                IsCaseSensitive = false
            };

            var otherFile = new SystemFile
            {
                FileName = "readme.md"
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Directories.AddRange(directory, otherParent, otherSubdirectory);
                dbContext.Files.Add(otherFile);

                await dbContext.SaveChangesAsync();
            });

            var content = new
            {
                data = new
                {
                    type = "systemDirectories",
                    id = directory.StringId,
                    attributes = new Dictionary<string, object>
                    {
                        ["name"] = "Project Files"
                    },
                    relationships = new Dictionary<string, object>
                    {
                        ["subdirectories"] = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "systemDirectories",
                                    id = otherSubdirectory.StringId
                                }
                            }
                        },
                        ["files"] = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "systemFiles",
                                    id = otherFile.StringId
                                }
                            }
                        },
                        ["parent"] = new
                        {
                            data = new
                            {
                                type = "systemDirectories",
                                id = otherParent.StringId
                            }
                        }
                    }
                }
            };

            string requestBody = JsonConvert.SerializeObject(content);
            string route = "/systemDirectories/" + directory.StringId;

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePatchAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.Data.Should().BeNull();
        }

        [Fact]
        public async Task When_patching_resource_with_multiple_self_references_it_must_succeed()
        {
            // Arrange
            var directory = new SystemDirectory
            {
                Name = "Projects",
                IsCaseSensitive = false
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Directories.Add(directory);
                await dbContext.SaveChangesAsync();
            });

            var content = new
            {
                data = new
                {
                    type = "systemDirectories",
                    id = directory.StringId,
                    attributes = new Dictionary<string, object>
                    {
                        ["name"] = "Project files"
                    },
                    relationships = new Dictionary<string, object>
                    {
                        ["self"] = new
                        {
                            data = new
                            {
                                type = "systemDirectories",
                                id = directory.StringId
                            }
                        },
                        ["alsoSelf"] = new
                        {
                            data = new
                            {
                                type = "systemDirectories",
                                id = directory.StringId
                            }
                        }
                    }
                }
            };

            string requestBody = JsonConvert.SerializeObject(content);
            string route = "/systemDirectories/" + directory.StringId;

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePatchAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.Data.Should().BeNull();
        }

        [Fact]
        public async Task When_patching_resource_with_collection_of_self_references_it_must_succeed()
        {
            // Arrange
            var directory = new SystemDirectory
            {
                Name = "Projects",
                IsCaseSensitive = false
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Directories.Add(directory);
                await dbContext.SaveChangesAsync();
            });

            var content = new
            {
                data = new
                {
                    type = "systemDirectories",
                    id = directory.StringId,
                    attributes = new Dictionary<string, object>
                    {
                        ["name"] = "Project files"
                    },
                    relationships = new Dictionary<string, object>
                    {
                        ["subdirectories"] = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "systemDirectories",
                                    id = directory.StringId
                                }
                            }
                        }
                    }
                }
            };

            string requestBody = JsonConvert.SerializeObject(content);
            string route = "/systemDirectories/" + directory.StringId;

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePatchAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.Data.Should().BeNull();
        }

        [Fact]
        public async Task When_patching_annotated_ToOne_relationship_it_must_succeed()
        {
            // Arrange
            var directory = new SystemDirectory
            {
                Name = "Projects",
                IsCaseSensitive = true,
                Parent = new SystemDirectory
                {
                    Name = "Data",
                    IsCaseSensitive = true
                }
            };

            var otherParent = new SystemDirectory
            {
                Name = "Data files",
                IsCaseSensitive = true
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Directories.AddRange(directory, otherParent);
                await dbContext.SaveChangesAsync();
            });

            var content = new
            {
                data = new
                {
                    type = "systemDirectories",
                    id = otherParent.StringId
                }
            };

            string requestBody = JsonConvert.SerializeObject(content);
            string route = "/systemDirectories/" + directory.StringId + "/relationships/parent";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePatchAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.Data.Should().BeNull();
        }

        [Fact]
        public async Task When_patching_annotated_ToMany_relationship_it_must_succeed()
        {
            // Arrange
            var directory = new SystemDirectory
            {
                Name = "Projects",
                IsCaseSensitive = true,
                Files = new List<SystemFile>
                {
                    new SystemFile
                    {
                        FileName = "Main.cs"
                    },
                    new SystemFile
                    {
                        FileName = "Program.cs"
                    }
                }
            };

            var otherFile = new SystemFile
            {
                FileName = "EntryPoint.cs"
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Directories.Add(directory);
                dbContext.Files.Add(otherFile);

                await dbContext.SaveChangesAsync();
            });

            var content = new
            {
                data = new[]
                {
                    new
                    {
                        type = "systemFiles",
                        id = otherFile.StringId
                    }
                }
            };

            string requestBody = JsonConvert.SerializeObject(content);
            string route = "/systemDirectories/" + directory.StringId + "/relationships/files";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePatchAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.Data.Should().BeNull();
        }
    }
}
