using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;

#pragma warning disable SKEXP0001
#pragma warning disable SKEXP0050
public class CustomTextEmbeddingGenerator : ITextEmbeddingGenerationService
{
    public IReadOnlyDictionary<string, object> Attributes => null;

    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;

    public CustomTextEmbeddingGenerator(IEmbeddingGenerator<string, Embedding<float>> generator)
    {
        _generator = generator;
    }

    public async Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(IList<string> data, Kernel kernel = null, CancellationToken cancellationToken = default)
    {
        var list = new List<ReadOnlyMemory<float>>();
        foreach (var item in data)
        {
            var embeddingForInput = await _generator.GenerateAsync(item);
            list.Add(embeddingForInput[0].Vector);
        }

        return list;
    }
}