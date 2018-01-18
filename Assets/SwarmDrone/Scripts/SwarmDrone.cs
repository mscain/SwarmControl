using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwarmDrone : MonoBehaviour{
    #region Variables
    public Vector3 flockingControlVector; //magnitude controls speed, direction controls turning

    public bool isControlledBySwarm = true;
    public float fwdControl, turnControl;
    public List<WheelCollider> wheelsR, wheelsL;
    public MeshRenderer headTexture;
    public float maxMoveSpeed = 20f;
    public float kick = 20;
    public float acceleration = 30f;
    public float turnSpeed = 50;
    Rigidbody rb;
    float velForward, velRight;
    Vector3 fwdVec, rightVec;
    Vector3 tangentVec, tangentRightVec;

    #endregion

    void Start(){
        rb = GetComponent<Rigidbody>();
        headTexture.material.color = Color.HSVToRGB(Random.Range(0, 360) / 360f, Random.Range(20, 100) / 100f, Random.Range(50, 100) / 100f);
    }

    void FixedUpdate(){
        if(isControlledBySwarm)
            FlockingControl();
        
        Move(fwdControl, turnControl);
    }

    /// <summary>
    /// Sets fwdControl and turnContol based on flocking algorithm.
    /// </summary>
    void FlockingControl(){
        flockingControlVector = Vector3.ClampMagnitude(flockingControlVector, 1);
        Debug.DrawRay(transform.position, flockingControlVector * 5);

        float fwdSpeedControl = Vector3.Dot(transform.forward, flockingControlVector.normalized); //Slows us down for turns
        fwdControl = (flockingControlVector.magnitude * fwdSpeedControl + flockingControlVector.magnitude)/2f;

        float turnSpeedControl = Mathf.Deg2Rad * Vector3.Angle(transform.forward, flockingControlVector); //Turns faster the larger the angle is, slows down as we approach desired heading
        float turnDirectionControl = Vector3.Dot(transform.right, flockingControlVector.normalized);
        turnDirectionControl = turnDirectionControl / Mathf.Abs(turnDirectionControl + 0.01f); //Decides which direction we should be turning
        turnControl = turnDirectionControl * turnSpeedControl;
    }


    /// <summary>
    /// Controls the bot movement. Should only be called once per physics timestep (last call will always override).
    /// </summary>
    /// <param name="fwdInput">Float between -1 and 1 that controls bots forward movement</param>
    /// <param name="turnInput">Float between -1 and 1 specifying how fast to turn. 1 is full right, -1 full left.</param>
    void Move(float fwdInput, float turnInput){
        foreach(WheelCollider wheel in wheelsR)
            wheel.motorTorque = fwdInput * acceleration - turnInput * turnSpeed;
        foreach(WheelCollider wheel in wheelsL)
            wheel.motorTorque = fwdInput * acceleration + turnInput * turnSpeed;
    }
}
