using UnityEngine;

public class CarInputController :    MonoBehaviour
{
    [SerializeField]    private Car car;

    [Header("Movement")]
    [SerializeField]    private KeyCode forwardKey = KeyCode.W;
    [SerializeField]    private KeyCode backwardKey = KeyCode.S;

    [Header("Rotation")]
    [SerializeField]    private KeyCode leftKey = KeyCode.A;
    [SerializeField]    private KeyCode rightKey = KeyCode.D;

    [Header("Jump")]
    [SerializeField]    private KeyCode jumpKey = KeyCode.Space;

    private void Update()
    {
        if (car == null)
            return;

        float movement = 0f;

        if (Input.GetKey(forwardKey))
            movement += 1f;

        if (Input.GetKey(backwardKey))
            movement -= 1f;

        float rotation = 0f;

        if (Input.GetKey(leftKey))
            rotation -= 1f;

        if (Input.GetKey(rightKey))
            rotation += 1f;

        car.SetMovementInput(movement);
        car.SetRotationInput(rotation);

        if (Input.GetKeyDown(jumpKey))
            car.Jump();
    }
}