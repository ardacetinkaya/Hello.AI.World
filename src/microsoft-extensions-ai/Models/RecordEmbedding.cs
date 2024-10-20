
public class RecordEmbedding<T>
{
    public string Title { get; set; }
    public T Record { get; set; }
    public float[] Embeddings { get; set; }
}
