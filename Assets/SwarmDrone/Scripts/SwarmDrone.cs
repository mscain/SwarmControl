using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwarmDrone : MonoBehaviour {
    #region Variables
    public float maxMoveSpeed = 20f;
    public float kick = 20;
    public float acceleration = 30f;
    public float turnSpeed = 50;
    Rigidbody rb;
    float velForward, velRight;
    Vector3 fwdVec, rightVec;
    Vector3 tangentVec, tangentRightVec;
    #endregion

	// Use this for initialization
	void Start () {
        rb = GetComponent<Rigidbody>();
	}
	
	// Update is called once per frame
	void Update () {
        Turn(-1f);
        Move(.5f);
	}

    /// <summary>
    /// Moves the bot forwards or backwards
    /// </summary>
    /// <param name="fwdInput">Float between -1 and 1 that controls bots forward movement</param>
    void Move(float fwdInput){
        velForward = Vector3.Dot(rb.velocity, transform.forward); //Current fwd speed. Used to see if Kick is necessary.
        velRight = Vector3.Dot(rb.velocity, transform.right);//Similar to ^

        fwdVec = transform.forward * acceleration * fwdInput * Time.deltaTime / 50 * rb.mass;
        if(Mathf.Abs(fwdInput) > 0 && Mathf.Abs(velForward) < maxMoveSpeed){
            rb.velocity += fwdVec;
        }

        //::Kick - If moving from standstill, gives a kick so moving is more responsive.
        if(fwdInput > 0 && velForward < maxMoveSpeed / 3){
            rb.AddRelativeForce(0, 0, kick / 10 * rb.mass);
            //Debug.Log("kickf"); //\\\\\\\\\\\\\\\\\\\\\\\\\\
        }else if(fwdInput < 0 && velForward > -maxMoveSpeed / 3){
            rb.AddRelativeForce(0, 0, -kick / 10 * rb.mass);
            //Debug.Log("kickb"); //\\\\\\\\\\\\\\\\\\\\\\\\\\
        }
    }

    /// <summary>
    /// Turns the bot left or right
    /// </summary>
    /// <param name="turnInput">Float between -1 and 1 specifying how fast to turn. 1 is full right, -1 full left.</param>
    void Turn(float turnInput){
        rb.AddRelativeTorque(0f, turnInput * turnSpeed, 0f, ForceMode.Acceleration);
    }
}
