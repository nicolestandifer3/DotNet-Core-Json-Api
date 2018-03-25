using System.Threading.Tasks;
using JsonApiDotNetCore.Builders;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Models.Operations;
using JsonApiDotNetCore.Serialization;

namespace JsonApiDotNetCore.Services.Operations.Processors
{
    public interface IUpdateOpProcessor<T> : IOpProcessor
        where T : class, IIdentifiable<int>
    { }

    public interface IUpdateOpProcessor<T, TId> : IOpProcessor
        where T : class, IIdentifiable<TId>
    { }

    public class UpdateOpProcessor<T> : UpdateOpProcessor<T, int>
        where T : class, IIdentifiable<int>
    {
        public UpdateOpProcessor(
            IUpdateService<T, int> service,
            IJsonApiDeSerializer deSerializer,
            IDocumentBuilder documentBuilder,
            IContextGraph contextGraph
        ) : base(service, deSerializer, documentBuilder, contextGraph)
        { }
    }

    public class UpdateOpProcessor<T, TId> : ICreateOpProcessor<T, TId>
         where T : class, IIdentifiable<TId>
    {
        private readonly IUpdateService<T, TId> _service;
        private readonly IJsonApiDeSerializer _deSerializer;
        private readonly IDocumentBuilder _documentBuilder;
        private readonly IContextGraph _contextGraph;

        public UpdateOpProcessor(
            IUpdateService<T, TId> service,
            IJsonApiDeSerializer deSerializer,
            IDocumentBuilder documentBuilder,
            IContextGraph contextGraph)
        {
            _service = service;
            _deSerializer = deSerializer;
            _documentBuilder = documentBuilder;
            _contextGraph = contextGraph;
        }

        public async Task<Operation> ProcessAsync(Operation operation)
        {
            if (string.IsNullOrWhiteSpace(operation?.DataObject?.Id?.ToString()))
                throw new JsonApiException(400, "The data.id parameter is required for replace operations");

            var model = (T)_deSerializer.DocumentToObject(operation.DataObject);
            var result = await _service.UpdateAsync(model.Id, model);

            var operationResult = new Operation
            {
                Op = OperationCode.update
            };

            operationResult.Data = _documentBuilder.GetData(
                _contextGraph.GetContextEntity(operation.GetResourceTypeName()),
                result);

            return operationResult;
        }
    }
}
