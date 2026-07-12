using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HoverScale : MonoBehaviour
{
    [SerializeField] private float targetScale = 1.08f;
    [SerializeField] private float speed = 12f;

    private Vector3 originScale;
    private float target = 1f;
    private Coroutine scaleCoroutine;

    private void Awake()
    {
        originScale = transform.localScale;
    }
    
    public void SetHovered(bool hovered)
    {
        target = hovered ? targetScale : 1f;
    }

    private void Update()
    {
        Vector3 target = originScale * this.target;
    transform.localScale = Vector3.Lerp(
        transform.localScale,
        target,
        Time.deltaTime * speed);

    }
}
