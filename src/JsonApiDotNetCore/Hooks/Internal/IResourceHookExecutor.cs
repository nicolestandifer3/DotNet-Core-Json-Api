using JsonApiDotNetCore.Hooks.Internal.Execution;
using JsonApiDotNetCore.Hooks.Internal.Traversal;
using JsonApiDotNetCore.Resources;

namespace JsonApiDotNetCore.Hooks.Internal
{
    /// <summary>
    /// Transient service responsible for executing Resource Hooks as defined 
    /// in <see cref="ResourceDefinition{TResource}"/>. see methods in 
    /// <see cref="IReadHookExecutor"/>, <see cref="IUpdateHookExecutor"/> and 
    /// <see cref="IOnReturnHookExecutor"/> for more information.
    /// 
    /// Uses <see cref="TraversalHelper"/> for traversal of nested resource data structures.
    /// Uses <see cref="HookExecutorHelper"/> for retrieving meta data about hooks, 
    /// fetching database values and performing other recurring internal operations.
    /// </summary>
    public interface IResourceHookExecutor : IReadHookExecutor, IUpdateHookExecutor, ICreateHookExecutor, IDeleteHookExecutor, IOnReturnHookExecutor { }
}
