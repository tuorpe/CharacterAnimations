using UnityEngine;

public class PlayerController_old : MonoBehaviour
{

    public float AnimatorSpeed = 1f;

    protected Animator animator => GetComponent<Animator>();
    private CharacterController characterController => GetComponent<CharacterController>();

    private float inputMultiplier = 0.5f;

    private bool groundedPlayer;    
    private float gravityValue = -9.81f;
    private float velocityY = 0;
    [SerializeField] private float DistanceToGround = 1f;
    [SerializeField] private LayerMask IKLayers;
    
    private Collider currentlyAimedCollider;

    public Transform transformLeftHand;
    public Transform transformRightHand;

    public Vector3 headTargetPosition;
    public Vector3 headCurrentPosition;

    public Vector3 leftHandTargetPosition;
    public Vector3 leftHandCurrentPosition;

    public Vector3 rightHandTargetPosition;
    public Vector3 rightHandCurrentPosition;

    private void Awake()
    {
        animator.speed = AnimatorSpeed;
        
        transformLeftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        transformRightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
    }

    private void Update()
    {
        var horizontalInput = Input.GetAxis("Horizontal");
        var verticalInput = Input.GetAxis("Vertical");

        //Is running?        
        inputMultiplier = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? 1f : 0.5f;

        animator.SetFloat("Horizontal", horizontalInput * inputMultiplier);
        animator.SetFloat("Vertical", verticalInput * inputMultiplier);

        
        var movementDirection = new Vector3(horizontalInput, 0, verticalInput);
        var inputMagnitude = Mathf.Clamp01(movementDirection.magnitude);

        animator.SetBool("IsMoving", inputMagnitude >= 0.01f);


        groundedPlayer = characterController.isGrounded;
        if (groundedPlayer && velocityY < 0)        
            velocityY = 0f;         
    }

    private void OnAnimatorMove()
    {
        var velocity = animator.deltaPosition;
        velocity.y += gravityValue * Time.deltaTime;
        characterController.Move(velocity);        
    }
    
    #region IK methods
    //Head movement can simply use SetLookAtPosition function
    private void SetHeadIK(Vector3 targetPosition)
    {
        animator.SetLookAtWeight(1f); //<-- prefer Awake/Start if static
        animator.SetLookAtPosition(targetPosition);
    }

    //Hands can follow individually target position, rotation is not needed and logic for 
    //activating IK and setting the target is done elsewhere.
    private void SetHandIKPos(AvatarIKGoal avatarIKHandGoal, Vector3 targetPosition)
    {
        animator.SetIKPositionWeight(avatarIKHandGoal, 1f); //<-- prefer Awake/Start if static      
        animator.SetIKPosition(avatarIKHandGoal, targetPosition);        
    }

    //Both feet function similarly, so the simple logic is included in this function:
    //raycast to ground (down from foot's current position), set target to hit position
    //and add some gap (because "foot" is actually ankle in rig)
    private void SetFootIKPos(AvatarIKGoal avatarIKFootGoal)
    {
        animator.SetIKPositionWeight(avatarIKFootGoal, 1f); //<-- prefer Awake/Start if static
        animator.SetIKRotationWeight(avatarIKFootGoal, 1f); //<-- prefer Awake/Start if static

        var groundRay = new Ray(animator.GetIKPosition(avatarIKFootGoal) + Vector3.up, Vector3.down);

        if (Physics.Raycast(groundRay, out var groundHit, DistanceToGround + 1f, IKLayers))
        {
            var footPos = groundHit.point;
            footPos.y += DistanceToGround;
            animator.SetIKPosition(avatarIKFootGoal, footPos);
        }
    }
    
    private void OnAnimatorIK(int layerIndex)
    {
         //Hard coded "look forward" head position
         headTargetPosition = transform.position + transform.forward + new Vector3(0,1.5f,0);
         
         //Find hand (and head) targets
        if (currentlyAimedCollider != null)
        {
            var directionToTarget = currentlyAimedCollider.bounds.center - transform.position;
            //Get xz angle diff
            var angle = Vector3.Angle(new Vector3(transform.forward.x, 0, transform.forward.z), new Vector3(directionToTarget.x, 0, directionToTarget.z));
            angle = Vector3.Cross(transform.forward, directionToTarget).y < 0 ? angle * -1f : angle; //Polarity from cross product y

            if (angle is >= 0 and < 60f) 
            {
                leftHandTargetPosition = transformLeftHand.position;
                headTargetPosition = rightHandTargetPosition = currentlyAimedCollider.bounds.center;
            }
            else if (angle < 0 && angle > -60f)
            {
                headTargetPosition = leftHandTargetPosition = currentlyAimedCollider.bounds.center;
                rightHandTargetPosition = transformRightHand.position;

            }
            else
            {
                leftHandTargetPosition = transformLeftHand.position;
                rightHandTargetPosition = transformRightHand.position;
            }
        }
        else
        {
            leftHandTargetPosition = transformLeftHand.position;
            rightHandTargetPosition = transformRightHand.position;
        }
         
         //Hand IK
         leftHandCurrentPosition = Vector3.Lerp(leftHandCurrentPosition, leftHandTargetPosition, Time.deltaTime * 10f);
         SetHandIKPos(AvatarIKGoal.LeftHand, leftHandCurrentPosition);

         rightHandCurrentPosition = Vector3.Lerp(rightHandCurrentPosition, rightHandTargetPosition, Time.deltaTime * 10f);
         SetHandIKPos(AvatarIKGoal.RightHand, rightHandCurrentPosition);

         //Foot IK
         SetFootIKPos(AvatarIKGoal.LeftFoot);
         SetFootIKPos(AvatarIKGoal.RightFoot);

         //Head IK
         headCurrentPosition = Vector3.Lerp(headCurrentPosition, headTargetPosition, Time.deltaTime * 10f);
         SetHeadIK(headCurrentPosition);
    }
    #endregion
    
    private void GetAimTarget()
    {
        if (Camera.main == null) 
            return;
        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out var hit))
            currentlyAimedCollider = hit.collider.gameObject.GetComponent<IShootable>()?.TakeHit();
    }
}
