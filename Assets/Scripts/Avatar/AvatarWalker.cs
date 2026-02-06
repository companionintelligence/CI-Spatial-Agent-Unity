using UnityEngine;

public class AvatarWalker : MonoBehaviour
{
    public float Radius = 1.5f;
    public float Speed = 0.6f;

    Vector3 _center;
    float _angle;

    void Start()
    {
        _center = transform.position;
    }

    void Update()
    {
        _angle += Speed * Time.deltaTime;
        var offset = new Vector3(Mathf.Cos(_angle), 0f, Mathf.Sin(_angle)) * Radius;
        var next = _center + offset;
        var forward = next - transform.position;

        transform.position = next;
        if (forward.sqrMagnitude > 0.0001f)
            transform.forward = forward.normalized;
    }
}
