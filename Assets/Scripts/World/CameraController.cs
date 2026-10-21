using UnityEngine;
using UnityEngine.Animations;

public class CameraController : MonoBehaviour
{
    [SerializeField] private GameObject protagEntity;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }

    // Update is called once per frame
    void LateUpdate()
    {
        transform.position = protagEntity.transform.position + new Vector3(0f, 0f, -10f);
    }
}
