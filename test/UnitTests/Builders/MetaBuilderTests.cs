//using System.Collections.Generic;
//using JsonApiDotNetCore.Builders;
//using Xunit;

//namespace UnitTests.Builders
//{
//    public class MetaBuilderTests
//    {
//        [Fact]
//        public void Can_Add_Key_Value()
//        {
//            // Arrange
//            var builder = new MetaBuilder();
//            var key = "test";
//            var value = "testValue";

//            // Act
//            builder.Add(key, value);
//            var result = builder.Build();

//            // Assert
//            Assert.NotEmpty(result);
//            Assert.Equal(value, result[key]);
//        }

//        [Fact]
//        public void Can_Add_Multiple_Values()
//        {
//            // Arrange
//            var builder = new MetaBuilder();
//            var input = new Dictionary<string, object> {
//            { "key1", "value1" },
//            { "key2", "value2" }
//           };

//            // Act
//            builder.Add(input);
//            var result = builder.Build();

//            // Assert
//            Assert.NotEmpty(result);
//            foreach (var entry in input)
//                Assert.Equal(input[entry.Key], result[entry.Key]);
//        }

//        [Fact]
//        public void When_Adding_Duplicate_Values_Keep_Newest()
//        {
//            // Arrange
//            var builder = new MetaBuilder();
            
//            var key = "key";
//            var oldValue = "oldValue";
//            var newValue = "newValue";
            
//            builder.Add(key, oldValue);

//            var input = new Dictionary<string, object> {
//                { key, newValue },
//                { "key2", "value2" }
//            };

//            // Act
//            builder.Add(input);
//            var result = builder.Build();

//            // Assert
//            Assert.NotEmpty(result);
//            Assert.Equal(input.Count, result.Count);
//            Assert.Equal(input[key], result[key]);
//        }
//    }
//}
