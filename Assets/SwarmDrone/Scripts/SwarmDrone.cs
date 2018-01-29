using JetBrains.Annotations; //TODO remove this and the CanBeNull tags since they might cause issues when sharing this script
using ExtensionMethods;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.Mathf;
using Random = UnityEngine.Random;

public class SwarmDrone : MonoBehaviour {
    #region Variables

    public bool isControlledBySwarm = true;
    public bool debug = true;
    public float proxMult, goalMult;
    public float scanRadius = 5f; //How far each bot scans for other bots
    public float swarmSpread = 1f; //How far each robot should stay from other robots
    public float swarmCohesion = .7f; //How much the swarm likes to stay together
    public float repulsionForce = 0.02f; //How strongly they should repel away
    public float fwdControl, turnControl;
    public Vector3 botMoveVector; //used if isControlledBySwarm
    public LayerMask layerMask; //what should we ignore for collision detection rays
    public List<WheelCollider> wheelsR, wheelsL;
    public MeshRenderer headTexture;
    public float speed = 50f;
    public float turnSpeed = 5f;
    public float turnSharpness = 0.3f; //defines how sharp turns should be when changing botMoveVector
    private const float UpdateFrequency = 30; //e.g. 10 times / second
    private Rigidbody _rb;
    private int _controlMode;
    [CanBeNull] private PlayerDrone _player;
    [CanBeNull] private Transform _carrot;

    #endregion

    private void Start() {
        _rb = GetComponent<Rigidbody>();
        _player = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerDrone>();
        _player?.swarm.Add(gameObject); //Add this bot to the swarm
        _carrot = GameObject.FindGameObjectWithTag("Carrot").transform;
        headTexture.material.color = Color.HSVToRGB(Random.Range(0f, 1f), Random.Range(.2f, 1f), Random.Range(.5f, 1f));

        InvokeRepeating(nameof(FlockingControl), 0, 1 / UpdateFrequency);
    }

    private void FixedUpdate() {
        if(isControlledBySwarm)
            SetMoveByVector(botMoveVector);
        Move(fwdControl, turnControl);
    }

    /// <summary>
    ///     Sets bot movement based on flocking algorithm.
    /// </summary>
    private void FlockingControl() {
        _controlMode = _player != null ? _player.swarmControlMode : 0;
        Vector3 flockingControlVector = proxMult * CalcProximalControl() + goalMult * CalcGoalControl();

        flockingControlVector = Vector3.ClampMagnitude(flockingControlVector, 1);
        if(isControlledBySwarm)
            botMoveVector = flockingControlVector;
    }

    private Vector3 CalcProximalControl() {
        Vector3 proxVec = Vector3.zero;

        //select headColliders within scanRadius of this bot
        foreach(Collider headCollider in Physics.OverlapSphere(transform.position, scanRadius, 1 << 11)) {
            Transform botT = headCollider.transform.parent;
            if(botT.gameObject.Equals(gameObject))
                continue; //Skip this bot

            float di = Vector3.Distance(transform.position, botT.position);
            if(Abs(di - swarmSpread) < swarmCohesion) {
                //They are at the right distance, so slow them down relative to each other
                proxVec += (headCollider.attachedRigidbody.velocity - _rb.velocity).normalized;
            }


            float sigma = swarmSpread / Pow(2, 1 / 6f);
            float eps = repulsionForce;
            float pidi = -8 * eps * (2 * Pow(sigma, 4) / Pow(di, 5) - Pow(sigma, 2) / Pow(di, 3));
            Vector3 phi = (botT.position - transform.position).normalized;

            proxVec += pidi * phi;
        }

        return proxVec;
    }

    private Vector3 CalcGoalControl() {
        Vector3 targetVec = Vector3.zero; //Where we want to go
        Vector3 goalVec = Vector3.zero; //Where we end up going

        switch(_controlMode) {
            case 1:
                if(_carrot != null) targetVec = Vector3.ClampMagnitude(_carrot.position - transform.position, 1);
                break;
            case 2:
                if(_player == null) break;

                targetVec = _player.swarmVec;
                Vector3 centerVec = Vector3.ClampMagnitude(_player.swarmCenterPos - transform.position, 1);
                if(Input.GetButton("Vertical") || Input.GetButton("Horizontal"))
                    targetVec += centerVec * 0.1f;
                else
                    targetVec += centerVec * 0.4f;

                break;
        }

        RaycastHit hit;
        Vector3 origin = transform.position;
        Vector3 fwd = transform.forward;
        float rayDist = _rb.velocity.magnitude * 0.3f + 0.2f;
        const float fMul = 10;
        const float tMul = 10;
        Vector3 right = transform.right;

        if(debug) {
            Debug.DrawRay(origin, fwd * rayDist, Color.red); //Front
            Debug.DrawRay(origin, -fwd * 0.1f, Color.red); //Back
            Debug.DrawRay(origin, (fwd + right * .5f) * rayDist, Color.red); //Diag Right
            Debug.DrawRay(origin, (fwd - right * .5f) * rayDist, Color.red); //Diag Left
            Debug.DrawRay(origin, right * .8f * rayDist, Color.red); //Right
            Debug.DrawRay(origin, -right * .8f * rayDist, Color.red); //Left
        }

        if(Physics.Raycast(origin, -fwd, out hit, 0.1f, layerMask)) { //Back
            int dirMult = (int) Time.time % 6 < 3 ? 1 : -1;
            goalVec += fwd + dirMult * right;

            if(debug) Debug.DrawRay(origin, -fwd * 0.1f, Color.yellow); //Back
        } else if(Physics.Raycast(origin, fwd + right * .5f, out hit, rayDist * .8f, layerMask)) { //Diag Right
            Vector3 diff = transform.position - hit.point;
            diff *= Vector3.Dot(diff, -fwd);
            float distMult = Lerp(fMul, fMul / 4, diff.sqrMagnitude);
            float turnMult = Lerp(tMul / 3, tMul, diff.sqrMagnitude);
            Vector3 turnVec = Vector3.Cross(transform.up, hit.normal);
            goalVec += hit.normal * distMult + turnVec * turnMult;
            if(Vector3.Dot(targetVec, fwd) < -.5f) goalVec += targetVec;

            if(!debug) return goalVec;
            Debug.DrawRay(origin, (fwd + right * .5f) * rayDist, Color.yellow); //Diag Right
            Debug.DrawRay(origin, diff, Color.cyan);
            Debug.DrawRay(origin, turnVec, Color.blue);
        } else if(Physics.Raycast(origin, fwd - right * .5f, out hit, rayDist * .8f, layerMask)) { //Diag Left
            Vector3 diff = transform.position - hit.point;
            diff *= Vector3.Dot(diff, -fwd);
            float distMult = Lerp(fMul, fMul / 4, diff.sqrMagnitude);
            float turnMult = Lerp(tMul / 3, tMul, diff.sqrMagnitude);
            Vector3 turnVec = Vector3.Cross(-transform.up, hit.normal);
            goalVec += hit.normal * distMult + turnVec * turnMult;
            if(Vector3.Dot(targetVec, fwd) < -.5f) goalVec += targetVec;

            if(!debug) return goalVec;
            Debug.DrawRay(origin, (fwd - right * .5f) * rayDist, Color.yellow); //Diag Left
            Debug.DrawRay(origin, diff, Color.cyan);
            Debug.DrawRay(origin, turnVec, Color.blue);
        } else if(Physics.Raycast(origin, right, out hit, rayDist, layerMask)) { //Right
            float turnMult = Vector3.Dot(hit.normal, -fwd);
            turnMult = turnMult > 0 ? turnMult : 0;
            goalVec += targetVec + fwd * 0.5f - right * turnMult;

            if(!debug) return goalVec;
            Debug.DrawRay(origin, right * .8f * rayDist, Color.yellow); //Right
        } else if(Physics.Raycast(origin, -right, out hit, rayDist, layerMask)) { //Left
            float turnMult = Vector3.Dot(hit.normal, -fwd);
            turnMult = turnMult > 0 ? turnMult : 0;
            goalVec += targetVec + fwd * 0.5f + right * turnMult;

            if(!debug) return goalVec;
            Debug.DrawRay(origin, -right * .8f * rayDist, Color.yellow); //Left
        } else if(Physics.Raycast(origin, fwd, out hit, rayDist, layerMask)) { //Front
            Vector3 diff = transform.position - hit.point;
            diff *= Vector3.Dot(diff, -fwd);
            float distMult = Lerp(fMul, .1f, diff.sqrMagnitude);
            float turnMult = Lerp(.1f, tMul, diff.sqrMagnitude) * Random.Range(-1, 1);
            Vector3 turnVec = Vector3.Cross(transform.up, diff.normalized);
            goalVec += hit.normal * distMult + turnVec * turnMult;

            if(!debug) return goalVec;
            Debug.DrawRay(origin, fwd * rayDist, Color.yellow); //Front
            Debug.DrawRay(origin, diff, Color.cyan);
        } else {
            goalVec += targetVec;
        }

        return goalVec;
    }

    /// <summary>
    ///     Sets bot movement based on a vector, where vector magnitude controls bot speed.
    /// </summary>
    /// <param name="moveVector">Vector that points where the bot should go, with magnitude between 0 and 1</param>
    private void SetMoveByVector(Vector3 moveVector) {
        if(debug) Debug.DrawRay(transform.position, moveVector / 2f, Color.green);

        //Slows us down for turns
        float fwdSpeedControl = Vector3.Dot(transform.forward, moveVector.normalized);
        //Controls the amount we slow down per turn
        fwdSpeedControl = Lerp(moveVector.magnitude, moveVector.magnitude * fwdSpeedControl, turnSharpness);
        //Prevents stopping too fast
        fwdControl = Extensions.SharpInDamp(fwdControl, fwdSpeedControl, .5f);

        //Turns faster the larger the angle is, slows down as we approach desired heading
        float turnSpeedControl = Deg2Rad * Vector3.Angle(transform.forward, moveVector);
        //Slows down turning if trying to turn too fast while moving too fast so we don't flip
        turnSpeedControl *= Lerp(1f, .5f, Lerp(0, fwdControl, turnSpeedControl / 3));

        //Decides which direction we should be turning
        float turnDirectionControl = Vector3.Dot(transform.right, moveVector.normalized);
        turnDirectionControl = turnDirectionControl / Abs(turnDirectionControl + 0.01f);

        turnControl = turnDirectionControl * turnSpeedControl;
    }

    /// <summary>
    ///     Controls the bot movement. Should only be called once per physics timestep (last call will always override).
    /// </summary>
    /// <param name="fwdInput">Float between -1 and 1 that controls bots forward movement</param>
    /// <param name="turnInput">Float between -1 and 1 specifying how fast to turn. 1 is full right, -1 full left.</param>
    private void Move(float fwdInput, float turnInput) {
        foreach(WheelCollider wheel in wheelsR)
            wheel.motorTorque = fwdInput * speed - turnInput * turnSpeed;
        foreach(WheelCollider wheel in wheelsL)
            wheel.motorTorque = fwdInput * speed + turnInput * turnSpeed;
    }
}