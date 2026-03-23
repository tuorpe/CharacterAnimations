using UnityEngine;

public class PlayerMovement : MonoBehaviour
{

    public float AnimatorSpeed = 1f;

    protected Animator animator => GetComponent<Animator>();
    private CharacterController characterController => GetComponent<CharacterController>();

    private float inputMultiplier = 0.5f;

    private bool groundedPlayer;    
    private float gravityValue = -9.81f;
    private float velocityY = 0;

    private void Awake()
    {
        animator.speed = AnimatorSpeed;
    }

    private void Update()
    {
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        //Is running?        
        inputMultiplier = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? 1f : 0.5f;

        animator.SetFloat("Horizontal", horizontalInput * inputMultiplier);
        animator.SetFloat("Vertical", verticalInput * inputMultiplier);

        
        Vector3 movementDirection = new Vector3(horizontalInput, 0, verticalInput);
        float inputMagnitude = Mathf.Clamp01(movementDirection.magnitude);

        animator.SetBool("IsMoving", inputMagnitude >= 0.01f);


        groundedPlayer = characterController.isGrounded;
        if (groundedPlayer && velocityY < 0)        
            velocityY = 0f;         
    }

    private void OnAnimatorMove()
    {
        Vector3 velocity = animator.deltaPosition;
        velocity.y += gravityValue * Time.deltaTime;
        characterController.Move(velocity);        
    }
}
