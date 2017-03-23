﻿using JsonApiDotNetCore.Models;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.Unit.Models
{
    public class AttributesEqualsTests
    {
        [Fact]
        public void HasManyAttribute_Equals_Returns_True_When_Same_Name()
        {
            var a = new HasManyAttribute("test");
            var b = new HasManyAttribute("test");

            Assert.Equal(a, b);
        }

        [Fact]
        public void HasManyAttribute_Equals_Returns_False_When_Different_Name()
        {
            var a = new HasManyAttribute("test");
            var b = new HasManyAttribute("test2");

            Assert.NotEqual(a, b);
        }

        [Fact]
        public void HasOneAttribute_Equals_Returns_True_When_Same_Name()
        {
            var a = new HasOneAttribute("test");
            var b = new HasOneAttribute("test");

            Assert.Equal(a, b);
        }

        [Fact]
        public void HasOneAttribute_Equals_Returns_False_When_Different_Name()
        {
            var a = new HasOneAttribute("test");
            var b = new HasOneAttribute("test2");

            Assert.NotEqual(a, b);
        }

        [Fact]
        public void AttrAttribute_Equals_Returns_True_When_Same_Name()
        {
            var a = new AttrAttribute("test");
            var b = new AttrAttribute("test");

            Assert.Equal(a, b);
        }

        [Fact]
        public void AttrAttribute_Equals_Returns_False_When_Different_Name()
        {
            var a = new AttrAttribute("test");
            var b = new AttrAttribute("test2");

            Assert.NotEqual(a, b);
        }

        [Fact]
        public void HasManyAttribute_Doesnt_Equal_HasOneAttribute_With_Same_Name()
        {
            RelationshipAttribute a = new HasManyAttribute("test");
            RelationshipAttribute b = new HasOneAttribute("test");

            Assert.NotEqual(a, b);
            Assert.NotEqual(b, a);
        }
    }
}
