using System;
using System.Collections;
using System.Collections.Generic;
using Managers;
using UnityEngine;

/// <summary>Owns physical documents in transit and returns undelivered ones on cancellation.</summary>
public sealed class DocumentStackTransfer : System.IDisposable
{
    private readonly DocumentStack source;
    private readonly List<GameObject> documents;
    private ArchiveManager archiveWithReservation;
    private Transform reservedPoint;
    public int Count => documents.Count;

    public DocumentStackTransfer(DocumentStack source, int count)
    {
        this.source = source;
        documents = source.TakeOrdinaryDocuments(count);
    }

    public IEnumerator DeliverToArchive(ArchiveManager archive, Transform carrier, Func<Vector3, IEnumerator> walk,
        StackHolder holder = null)
    {
        if (source == null || archive == null || archive.OwnsStack(source)) yield break;
        int stackSize = source.maxStackSize;
        while (Count > 0 && archive != null && carrier != null)
        {
            reservedPoint = archive.RequestDropOffPoint();
            if (reservedPoint == null) yield break;
            archiveWithReservation = archive;
            try
            {
                holder?.ShowStack(Count, stackSize);
                yield return walk(reservedPoint.position);
                if (archive == null || reservedPoint == null ||
                    !ArchiveManager.IsWithinReach(carrier, reservedPoint.position)) yield break;

                // Two couriers can approach the main stack together. Recheck its capacity at arrival.
                var stack = archive.GetDropOffStack(reservedPoint);
                int slots = archive.AvailableDropOffSlots(stack);
                if (slots > 0 && DeliverTo(stack, slots) == 0) yield break;
            }
            finally
            {
                ReleaseDropOff();
            }
        }
    }

    private void ReleaseDropOff()
    {
        if (archiveWithReservation != null && reservedPoint != null)
            archiveWithReservation.FreeOverflowPoint(reservedPoint);
        archiveWithReservation = null;
        reservedPoint = null;
    }

    public int DeliverTo(DocumentStack destination) => DeliverTo(destination, int.MaxValue);

    private int DeliverTo(DocumentStack destination, int limit)
    {
        int delivered = 0;
        for (int i = documents.Count - 1; i >= 0 && delivered < limit; i--)
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
                UnityEngine.Object.Destroy(document);
            }
        documents.Clear();
    }

    public void Dispose()
    {
        ReleaseDropOff();
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
