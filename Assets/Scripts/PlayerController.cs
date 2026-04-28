using System;
using UnityEngine;
using UnityEngine.LowLevelPhysics2D;
using UnityEngine.Timeline;

public class PlayerController : MonoBehaviour
{
    public float AnimatorSpeed = 1f;

    protected Animator animator => GetComponent<Animator>();
    private CharacterController characterController => GetComponent<CharacterController>();

    private float inputMultiplier = 0.5f;

    private bool groundedPlayer;
    private float gravityValue = -9.81f;
    private float velocityY = 0;
    [SerializeField] private float DistanceToGround = 2f;
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
    
    [SerializeField] private float handIKDisableDelay = 2f;
    
    [Header("Timeline")]
    [SerializeField] private bool timelineActive = false;
    [SerializeField, Range(-1f, 1f)] private float timelineHorizontal;
    [SerializeField, Range(-1f, 1f)] private float timelineVertical;
    
    [Header("Debug")]
    [SerializeField]float horizontalInput;
    [SerializeField]float verticalInput;
    [SerializeField, Range(1f, 10f)] private float movementSpeedChangeMultiplier = 1f;
    [SerializeField] private Transform rubberDuck;
    [SerializeField] private bool isWatchingRubberDuck = false;

    private Vector3 targetPosition;
    [SerializeField, Range(0,1f)] private float leftHandIKDisableDelay;
    [SerializeField, Range(0,1f)] private float rightHandIKDisableDelay;
    private void Awake()
    {
        animator.speed = AnimatorSpeed;
        
        transformLeftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        transformRightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
    }

    private void Update()
    {

        //Is running?        
        inputMultiplier = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? 1f : 0.5f;

        if (!timelineActive)
        {
            horizontalInput = Input.GetAxis("Horizontal");
            verticalInput = Input.GetAxis("Vertical");

            horizontalInput *= inputMultiplier;
            verticalInput *= inputMultiplier;
        }
        else
        {
            horizontalInput = Mathf.MoveTowards(horizontalInput, timelineHorizontal, movementSpeedChangeMultiplier * Time.deltaTime);
            verticalInput = Mathf.MoveTowards(verticalInput, timelineVertical, movementSpeedChangeMultiplier * Time.deltaTime);
            
        }
        animator.SetFloat("Horizontal", horizontalInput);
        animator.SetFloat("Vertical", verticalInput);
        
        var movementDirection = new Vector3(horizontalInput, 0, verticalInput);
        var inputMagnitude = Mathf.Clamp01(movementDirection.magnitude);
        animator.SetBool("IsMoving", inputMagnitude >= 0.01f);


        groundedPlayer = characterController.isGrounded;
        if (groundedPlayer && velocityY < 0)        
            velocityY = 0f;

        GetAimTarget();
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
    private void SetHandIKPos(AvatarIKGoal avatarIKHandGoal, Vector3 targetPosition, bool isAiming = false)
    {
        if (isAiming)
        {
            Vector3 dir;
            Quaternion offset;
            if (avatarIKHandGoal == AvatarIKGoal.LeftHand)
            {
                dir = (targetPosition - transformLeftHand.position).normalized;
                offset = Quaternion.Euler(0, 0, 90f);
                leftHandIKDisableDelay = 1f;
            }
            else
            {
                dir = (targetPosition - transformRightHand.position).normalized;
                offset = Quaternion.Euler(0, 0, -90f);
                rightHandIKDisableDelay = 1f;
            }
            var rot = Quaternion.LookRotation(dir, Vector3.up) * offset;
            animator.SetIKPositionWeight(avatarIKHandGoal, 1f); //<-- prefer Awake/Start if static
            animator.SetIKRotationWeight(avatarIKHandGoal, 1f); //<-- prefer Awake/Start if static
            animator.SetIKRotation(avatarIKHandGoal, rot);
        }
        else
        {
            leftHandIKDisableDelay = Mathf.MoveTowards(leftHandIKDisableDelay, 0, Time.deltaTime/handIKDisableDelay);
            rightHandIKDisableDelay = Mathf.MoveTowards(rightHandIKDisableDelay, 0, Time.deltaTime/handIKDisableDelay);
            animator.SetIKPositionWeight(avatarIKHandGoal,
                avatarIKHandGoal == AvatarIKGoal.LeftHand ? leftHandIKDisableDelay : rightHandIKDisableDelay);
            animator.SetIKRotationWeight(avatarIKHandGoal, 0f);
        }
        
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
        var leftIsAiming = false;
        var rightIsAiming = false;
         //Hard coded "look forward" head position
         if(!isWatchingRubberDuck)
             headTargetPosition = transform.position + transform.forward + new Vector3(0,1.5f,0);
        else
             headTargetPosition = Vector3.Lerp(headTargetPosition, rubberDuck.position, Time.deltaTime * 5f);
         //Find hand (and head) targets
        if (currentlyAimedCollider != null)
        {
            var directionToTarget = currentlyAimedCollider.bounds.center - transform.position;
            //Get xz angle diff
            var angle = Vector3.Angle(new Vector3(transform.forward.x, 0, transform.forward.z), new Vector3(directionToTarget.x, 0, directionToTarget.z));
            angle = Vector3.Cross(transform.forward, directionToTarget).y < 0 ? angle * -1f : angle; //Polarity from cross product y

            switch (angle)
            {
                case >= 0 and < 60f:
                    leftHandTargetPosition = transformLeftHand.position;
                    headTargetPosition = rightHandTargetPosition = currentlyAimedCollider.bounds.center;
                    rightIsAiming = true;
                    break;
                case < 0 and > -60f:
                    headTargetPosition = leftHandTargetPosition = currentlyAimedCollider.bounds.center;
                    rightHandTargetPosition = transformRightHand.position;
                    leftIsAiming = true;
                    break;
                default:
                    leftHandTargetPosition = transformLeftHand.position;
                    rightHandTargetPosition = transformRightHand.position;
                    break;
            }
        }
        else
        {
            leftHandTargetPosition = transformLeftHand.position;
            rightHandTargetPosition = transformRightHand.position;
        }
         
        //Hand IK
        leftHandCurrentPosition = Vector3.Lerp(leftHandCurrentPosition, leftHandTargetPosition, Time.deltaTime * 5f);
        SetHandIKPos(AvatarIKGoal.LeftHand, leftHandCurrentPosition, leftIsAiming);

        rightHandCurrentPosition = Vector3.Lerp(rightHandCurrentPosition, rightHandTargetPosition, Time.deltaTime * 5f);
        SetHandIKPos(AvatarIKGoal.RightHand, rightHandCurrentPosition, rightIsAiming);
         
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
        targetPosition = Vector3.zero;
        if (Physics.Raycast(ray, out var hit))
        {
            currentlyAimedCollider = hit.collider.gameObject.GetComponent<IShootable>()?.TakeHit();
            targetPosition = hit.point;
        }
    }

    public void SetHorMovement(float move)
    {
        timelineHorizontal = move;
    }
    public void SetVerMovement(float move)
    {
        timelineVertical = move;
    }

    public void WatchTheDuck(bool enable)
    {
        isWatchingRubberDuck = enable;
    }
    
    public void SetTimelineActive(bool active)
    {
        timelineActive = active;
    }
}
