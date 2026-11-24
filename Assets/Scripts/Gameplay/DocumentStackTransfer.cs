using System.Collections.Generic;
using UnityEngine;

/// <summary>Owns physical documents in transit and returns undelivered ones on cancellation.</summary>
public sealed class DocumentStackTransfer : System.IDisposable
{
    private readonly DocumentStack source;
    private readonly List<GameObject> documents;
    public int Count => documents.Count;

    public DocumentStackTransfer(DocumentStack source, int count)
    {
        this.source = source;
        documents = source.TakeOrdinaryDocuments(count);
    }

    public int DeliverTo(DocumentStack destination)
    {
        int delivered = 0;
        for (int i = documents.Count - 1; i >= 0; i--)
        {
            if (destination == null || !destination.ReceiveExistingDocument(documents[i])) break;
            documents.RemoveAt(i);
            delivered++;
        }
        return delivered;
    }

    public void Archive()
    {
        foreach (var document in documents)
            if (document != null)
            {
                DocumentStack.CompleteTransit(document);
                Object.Destroy(document);
            }
        documents.Clear();
    }

    public void Dispose()
    {
        foreach (var document in documents)
        {
            if (document == null) continue;
            if (source != null) source.ReceiveExistingDocument(document, returningToSource: true);
            else
            {
                // Keep a visible document if its original desk was removed during transport.
                document.SetActive(true);
                Debug.LogWarning("[DocumentTransfer] Source desk disappeared; document retained in scene.", document);
            }
        }
        documents.Clear();
    }
}
