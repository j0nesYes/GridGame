using UnityEngine;
using System;

public class RemoteTriggerZone : MonoBehaviour
{
    public event Action<Collider2D> OnObjectEnteredTrigger;
    public event Action<Collider2D> OnObjectExitTrigger;

    [HideInInspector] public Collider2D trigger;

    private void Awake()
    {
        trigger = GetComponent<Collider2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        OnObjectEnteredTrigger?.Invoke(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        OnObjectExitTrigger?.Invoke(other);
    }
}
