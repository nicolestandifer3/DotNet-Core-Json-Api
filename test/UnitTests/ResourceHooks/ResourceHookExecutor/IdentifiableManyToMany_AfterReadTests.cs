//using JsonApiDotNetCore.Builders;
//using JsonApiDotNetCore.Services;
//using JsonApiDotNetCoreExample.Models;
//using Moq;
//using System.Collections.Generic;
//using System.Linq;
//using Xunit;
//using Bogus;

//namespace UnitTests.ResourceHooks
//{

//    public class IdentifiableManyToMany_AfterReadTests : ResourceHooksTestBase
//    {
//        public IdentifiableManyToMany_AfterReadTests()
//        {
//            // Build() exposes the static ResourceGraphBuilder.Instance member, which 
//            // is consumed by ResourceDefinition class.
//            new ResourceGraphBuilder()
//                .AddResource<Article>()
//                .AddResource<IdentifiableArticleTag>()
//                .AddResource<Tag>()
//                .Build();
//        }

//        (List<Article>, List<IdentifiableArticleTag>, List<Tag>) CreateDummyData()
//        {
//            var tagsSubset = new Faker<Tag>().Generate(3).ToList();
//            var joinsSubSet = new Faker<IdentifiableArticleTag>().Generate(3).ToList();
//            var articleTagsSubset = new Article() { IdentifiableArticleTags = joinsSubSet };
//            for (int i = 0; i < 3; i++)
//            {
//                joinsSubSet[i].Article = articleTagsSubset;
//                joinsSubSet[i].Tag = tagsSubset[i];
//            }
//            var allTags = new Faker<Tag>().Generate(3).ToList().Concat(tagsSubset).ToList();
//            var completeJoin = new Faker<IdentifiableArticleTag>().Generate(6).ToList();
//            var articleWithAllTags = new Article() { IdentifiableArticleTags = completeJoin };

//            for (int i = 0; i < 6; i++)
//            {
//                completeJoin[i].Article = articleWithAllTags;
//                completeJoin[i].Tag = allTags[i];
//            }

//            var allJoins = joinsSubSet.Concat(completeJoin).ToList();
//            var articles = new List<Article>() { articleTagsSubset, articleWithAllTags };
//            return (articles, allJoins, allTags);
//        }


//        [Fact]
//        public void AfterRead()
//        {
//            // arrange
//            var articleDiscovery = SetDiscoverableHooks<Article>();
//            var joinDiscovery = SetDiscoverableHooks<IdentifiableArticleTag>();
//            var tagDiscovery = SetDiscoverableHooks<Tag>();

//            (var contextMock, var hookExecutor, var articleResourceMock,
//                var joinResourceMock, var tagResourceMock) = CreateTestObjects(articleDiscovery, joinDiscovery, tagDiscovery);

//            (var articles, var joins, var tags) = CreateDummyData();

//            // act
//            hookExecutor.AfterRead(articles, ResourceAction.Get);

//            // assert
//            articleResourceMock.Verify(rd => rd.AfterRead(It.IsAny<IEnumerable<Article>>(), It.IsAny<HookExecutionContext<Article>>()), Times.Once());
//            articleResourceMock.VerifyNoOtherCalls();

//            joinResourceMock.Verify(rd => rd.BeforeRead(It.IsAny<HookExecutionContext<IdentifiableArticleTag>>(), null), Times.Once());
//            joinResourceMock.Verify(rd => rd.AfterRead(It.Is<IEnumerable<IdentifiableArticleTag>>((collection) => !collection.Except(joins).Any()), It.IsAny<HookExecutionContext<IdentifiableArticleTag>>()), Times.Once());
//            joinResourceMock.VerifyNoOtherCalls();

//            tagResourceMock.Verify(rd => rd.BeforeRead(It.IsAny<HookExecutionContext<Tag>>(), null), Times.Once());
//            tagResourceMock.Verify(rd => rd.AfterRead(It.Is<IEnumerable<Tag>>((collection) => !collection.Except(tags).Any()), It.IsAny<HookExecutionContext<Tag>>()), Times.Once());
//            tagResourceMock.VerifyNoOtherCalls();
//        }

//        [Fact]
//        public void AfterRead_Without_Parent_Hook_Implemented()
//        {
//            // arrange
//            var articleDiscovery = SetDiscoverableHooks<Article>(new ResourceHook[0]);
//            var joinDiscovery = SetDiscoverableHooks<IdentifiableArticleTag>();
//            var tagDiscovery = SetDiscoverableHooks<Tag>();

//            (var contextMock, var hookExecutor, var articleResourceMock,
//                var joinResourceMock, var tagResourceMock) = CreateTestObjects(articleDiscovery, joinDiscovery, tagDiscovery);

//            (var articles, var joins, var tags) = CreateDummyData();

//            // act
//            hookExecutor.AfterRead(articles, ResourceAction.Get);

//            // assert
//            articleResourceMock.VerifyNoOtherCalls();

//            joinResourceMock.Verify(rd => rd.BeforeRead(It.IsAny<HookExecutionContext<IdentifiableArticleTag>>(), null), Times.Once());
//            joinResourceMock.Verify(rd => rd.AfterRead(It.Is<IEnumerable<IdentifiableArticleTag>>((collection) => !collection.Except(joins).Any()), It.IsAny<HookExecutionContext<IdentifiableArticleTag>>()), Times.Once());
//            joinResourceMock.VerifyNoOtherCalls();

//            tagResourceMock.Verify(rd => rd.BeforeRead(It.IsAny<HookExecutionContext<Tag>>(), null), Times.Once());
//            tagResourceMock.Verify(rd => rd.AfterRead(It.Is<IEnumerable<Tag>>((collection) => !collection.Except(tags).Any()), It.IsAny<HookExecutionContext<Tag>>()), Times.Once());
//            tagResourceMock.VerifyNoOtherCalls();
//        }

//        [Fact]
//        public void AfterRead_Without_Children_Before_Hooks_Implemented()
//        {
//            // arrange
//            var articleDiscovery = SetDiscoverableHooks<Article>();
//            var joinDiscovery = SetDiscoverableHooks<IdentifiableArticleTag>(new ResourceHook[] { ResourceHook.AfterRead });
//            var tagDiscovery = SetDiscoverableHooks<Tag>(new ResourceHook[] { ResourceHook.AfterRead });

//            (var contextMock, var hookExecutor, var articleResourceMock,
//                var joinResourceMock, var tagResourceMock) = CreateTestObjects(articleDiscovery, joinDiscovery, tagDiscovery);

//            (var articles, var joins, var tags) = CreateDummyData();

//            // act
//            hookExecutor.AfterRead(articles, ResourceAction.Get);

//            // assert
//            articleResourceMock.Verify(rd => rd.AfterRead(articles, It.IsAny<HookExecutionContext<Article>>()), Times.Once());
//            articleResourceMock.VerifyNoOtherCalls();

//            joinResourceMock.Verify(rd => rd.AfterRead(It.Is<IEnumerable<IdentifiableArticleTag>>((collection) => !collection.Except(joins).Any()), It.IsAny<HookExecutionContext<IdentifiableArticleTag>>()), Times.Once());
//            joinResourceMock.VerifyNoOtherCalls();

//            tagResourceMock.Verify(rd => rd.AfterRead(It.Is<IEnumerable<Tag>>((collection) => !collection.Except(tags).Any()), It.IsAny<HookExecutionContext<Tag>>()), Times.Once());
//            tagResourceMock.VerifyNoOtherCalls();
//        }

//        [Fact]
//        public void AfterRead_Without_Children_After_Hooks_Implemented()
//        {
//            // arrange
//            var articleDiscovery = SetDiscoverableHooks<Article>();
//            var joinDiscovery = SetDiscoverableHooks<IdentifiableArticleTag>(new ResourceHook[] { ResourceHook.BeforeRead });
//            var tagDiscovery = SetDiscoverableHooks<Tag>(new ResourceHook[] { ResourceHook.BeforeRead });

//            (var contextMock, var hookExecutor, var articleResourceMock,
//                var joinResourceMock, var tagResourceMock) = CreateTestObjects(articleDiscovery, joinDiscovery, tagDiscovery);

//            (var articles, var joins, var tags) = CreateDummyData();

//            // act
//            hookExecutor.AfterRead(articles, ResourceAction.Get);

//            // assert
//            articleResourceMock.Verify(rd => rd.AfterRead(articles, It.IsAny<HookExecutionContext<Article>>()), Times.Once());
//            articleResourceMock.VerifyNoOtherCalls();

//            joinResourceMock.Verify(rd => rd.BeforeRead(It.IsAny<HookExecutionContext<IdentifiableArticleTag>>(), null), Times.Once());
//            joinResourceMock.VerifyNoOtherCalls();

//            tagResourceMock.Verify(rd => rd.BeforeRead(It.IsAny<HookExecutionContext<Tag>>(), null), Times.Once());
//            tagResourceMock.VerifyNoOtherCalls();
//        }

//        [Fact]
//        public void AfterRead_Without_Any_Children_Hooks_Implemented()
//        {
//            // arrange
//            var articleDiscovery = SetDiscoverableHooks<Article>();
//            var joinDiscovery = SetDiscoverableHooks<IdentifiableArticleTag>(new ResourceHook[0]);
//            var tagDiscovery = SetDiscoverableHooks<Tag>(new ResourceHook[0]);

//            (var contextMock, var hookExecutor, var articleResourceMock,
//                var joinResourceMock, var tagResourceMock) = CreateTestObjects(articleDiscovery, joinDiscovery, tagDiscovery);

//            (var articles, var joins, var tags) = CreateDummyData();

//            // act
//            hookExecutor.AfterRead(articles, ResourceAction.Get);

//            // assert
//            articleResourceMock.Verify(rd => rd.AfterRead(articles, It.IsAny<HookExecutionContext<Article>>()), Times.Once());
//            articleResourceMock.VerifyNoOtherCalls();

//            joinResourceMock.VerifyNoOtherCalls();
//            tagResourceMock.VerifyNoOtherCalls();
//        }

//        [Fact]
//        public void AfterRead_Without_Any_Hook_Implemented()
//        {
//            // arrange
//            var articleDiscovery = SetDiscoverableHooks<Article>(new ResourceHook[0]);
//            var joinDiscovery = SetDiscoverableHooks<IdentifiableArticleTag>(new ResourceHook[0]);
//            var tagDiscovery = SetDiscoverableHooks<Tag>(new ResourceHook[0]);

//            (var contextMock, var hookExecutor, var articleResourceMock,
//                var joinResourceMock, var tagResourceMock) = CreateTestObjects(articleDiscovery, joinDiscovery, tagDiscovery);

//            (var articles, var joins, var tags) = CreateDummyData();

//            // act
//            hookExecutor.AfterRead(articles, ResourceAction.Get);

//            // assert
//            articleResourceMock.VerifyNoOtherCalls();
//            joinResourceMock.VerifyNoOtherCalls();
//            tagResourceMock.VerifyNoOtherCalls();
//        }
//    }
//}

