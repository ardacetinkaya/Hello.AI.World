using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;

#pragma warning disable SKEXP0001
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

        var embeddingForInputs = await _generator.GenerateAsync(data);
        
        foreach(var embeddingForInput in embeddingForInputs){
            list.Add(embeddingForInput.Vector);   
        }


        return list;
    }
}