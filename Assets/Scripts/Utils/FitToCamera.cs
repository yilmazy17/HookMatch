using UnityEngine;

public class FitToCamera : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;

    private SpriteRenderer sr;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (targetCamera == null) targetCamera = Camera.main;
        Fit();
    }

    private void Fit()
    {
        float camHeight = targetCamera.orthographicSize * 2f;
        float camWidth = camHeight * targetCamera.aspect;

        float spriteHeight = sr.sprite.bounds.size.y;
        float spriteWidth = sr.sprite.bounds.size.x;

        float scaleX = camWidth / spriteWidth;
        float scaleY = camHeight / spriteHeight;

        transform.localScale = new Vector3(scaleX, scaleY, 1f);
    }
}