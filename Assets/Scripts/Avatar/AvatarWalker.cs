using UnityEngine;

public class AvatarWalker : MonoBehaviour
{
    public float Radius = 1.5f;
    public float Speed = 0.6f;
    public bool CycleIdle = true;
    public float WalkDuration = 6f;
    public float IdleDuration = 2f;

    Vector3 _center;
    float _angle;
    float _timer;
    bool _isIdle;
    AvatarAnimationMixer _mixer;

    void Start()
    {
        _center = transform.position;
        _mixer = GetComponent<AvatarAnimationMixer>();
        if (_mixer == null)
            _mixer = GetComponentInChildren<AvatarAnimationMixer>();
    }

    void Update()
    {
        if (CycleIdle)
        {
            _timer += Time.deltaTime;
            if (_isIdle && _timer >= IdleDuration)
            {
                _isIdle = false;
                _timer = 0f;
                _mixer?.PlayCategory(AvatarAnimCategory.Locomotion);
            }
            else if (!_isIdle && _timer >= WalkDuration)
            {
                _isIdle = true;
                _timer = 0f;
                _mixer?.PlayCategory(AvatarAnimCategory.Idle);
            }
        }

        float speed = _isIdle ? 0f : Speed;
        _angle += speed * Time.deltaTime;
        var offset = new Vector3(Mathf.Cos(_angle), 0f, Mathf.Sin(_angle)) * Radius;
        var next = _center + offset;
        var forward = next - transform.position;

        transform.position = next;
        if (forward.sqrMagnitude > 0.0001f)
            transform.forward = forward.normalized;
    }
}
