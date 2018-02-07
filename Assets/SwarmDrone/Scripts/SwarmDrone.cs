using ExtensionMethods;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.Mathf;
using Random = UnityEngine.Random;

public class SwarmDrone : MonoBehaviour {
    #region Variables

    public bool isControlledBySwarm = true; //should we control this bot with swarm dynamics (useful for testing)
    public bool debug = true; //should we display debug rays for this bot
    public bool isAlive = true; //is the bot upright and on its wheels?
    public float proxMult, goalMult; //how much weight to give the different control vectors
    public float scanRadius = 5f; //How far each bot scans for other bots
    public float swarmSpread = 1f; //How far each robot should stay from other robots
    public float swarmCohesion = .7f; //How much the swarm likes to stay together
    public float repulsionForce = 0.02f; //How strongly they should repel away
    public float fwdControl, turnControl; //the direct control for the motors, making the bot go forward and/or turn
    public Vector3 botMoveVector; //direction where the bot should move
    public LayerMask layerMask; //what should we ignore for collision detection rays
    public List<WheelCollider> wheelsR, wheelsL; //the wheels on the bot
    public MeshRenderer headTexture; //the bots head texture, used to give them some differentiation
    public float speed = 50f; //how fast can the bot move
    public float turnSpeed = 5f; //how fast can the bot turn
    public float turnSharpness = 0.3f; //defines how sharp turns should be when changing botMoveVector

    [HideInInspector] public bool isLeader;

    private const float UpdateFrequency = 30; //how frequently should the bot update its vectors. e.g. 30 times / second
    private Rigidbody _rb; //the bots rigidbody (for convenience)
    private const float UpsideDownMax = 1.0f; //how long can the bot be upside down before it considers itself dead
    private float _upsideDownTimer = UpsideDownMax; //how long has the bot actually been upside down
    private PlayerDrone _player; //the player script
    private int _controlMode; //what control mode is the player using
    private Transform _carrot; //the carrot object's position
    private bool _inSwarm = true; //is this bot currently part of the players swarm

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
        //Determine if this bot should consider itself alive
        if(!IsGrounded())
            _upsideDownTimer -= Time.fixedDeltaTime;
        else
            _upsideDownTimer = UpsideDownMax;
        isAlive = _upsideDownTimer > 0;

        if(isAlive && !_inSwarm) { //Add this bot to the swarm
            _player?.swarm.Add(gameObject);
            foreach(var coll in GetComponentsInChildren<Collider>()) {
                coll.gameObject.layer = 12;
            }
            headTexture.gameObject.layer = 11;
            _inSwarm = true;
        } else if(!isAlive && _inSwarm) { //Remove this bot from the swarm
            _player?.swarm.Remove(gameObject);
            if(isLeader) {
                _player?.DeelectLeader();
                StartCoroutine(_player?.ElectLeader());
            }
            foreach(var coll in GetComponentsInChildren<Collider>()) {
                coll.gameObject.layer = 0;
            }
            _inSwarm = false;
        }

        if(_player != null) swarmSpread = _player.swarmSpread;

        if(isControlledBySwarm && !isLeader)
            SetMoveByVector(botMoveVector); //set the bot's controls based on botMoveVector

        Move(fwdControl, turnControl); //turn the bots wheels based on its controls
    }

    /// <summary> Sets bot movement based on flocking algorithm. </summary>
    private void FlockingControl() {
        _controlMode = _player != null ? _player.swarmControlMode : 0;
        Vector3 flockingControlVector = proxMult * CalcProximalControl() + goalMult * CalcGoalControl();

        flockingControlVector = Vector3.ClampMagnitude(flockingControlVector, 1);
        if(isControlledBySwarm)
            botMoveVector = flockingControlVector;
    }

    /// <summary> Calculates the vector that keeps the bot in swarm formation </summary>
    private Vector3 CalcProximalControl() {
        Vector3 proxVec = Vector3.zero;

        //select headColliders within scanRadius of this bot
        foreach(Collider headCollider in Physics.OverlapSphere(transform.position, scanRadius, 1 << 11)) {
            Transform botT = headCollider.transform.parent;
            if(botT.gameObject.Equals(gameObject) || !botT.GetComponent<SwarmDrone>().isAlive)
                continue; //Skip this bot

            float di = Vector3.Distance(transform.position, botT.position);
            if(Abs(di - swarmSpread) < swarmCohesion) {
                //They are at the right distance, so slow them down relative to each other
                proxVec += (headCollider.attachedRigidbody.velocity - _rb.velocity).normalized;
            }

            //Formula for swarm attraction/repulsion. Not really sure how this works, but it does.
            float sigma = swarmSpread / Pow(2, 1 / 6f);
            float eps = repulsionForce;
            float pidi = -8 * eps * (2 * Pow(sigma, 4) / Pow(di, 5) - Pow(sigma, 2) / Pow(di, 3));
            Vector3 phi = (botT.position - transform.position).normalized;

            proxVec += pidi * phi;
        }

        return proxVec;
    }

    /// <summary> Calculates the vector that decides where this bot wants to go, as well as collision avoidance </summary>
    private Vector3 CalcGoalControl() {
        Vector3 goalVec = Vector3.zero; //Where we want to go

        //Set the target based on the current control mode being used
        switch(_controlMode) {
            case 1: //Carrot
                if(_carrot != null) goalVec = Vector3.ClampMagnitude(_carrot.position - transform.position, 1);
                break;
            case 2: //Direct control from COM
            case 3: //Direct control from center of shape
                if(_player == null) break;
                goalVec = _player.swarmVec;
                Vector3 centerVec = Vector3.ClampMagnitude(_player.swarmCenterPos - transform.position, 1);
                if(Input.GetButton("Vertical") || Input.GetButton("Horizontal"))
                    goalVec += centerVec * 0.1f;
                else
                    goalVec += centerVec * 0.4f;
                break;
            case 4: //Leader w/ carrot
            case 5: //Leader w/ carrot behind
            case 6: //Leader w/ carrot on chain behind
                if(_carrot != null && !isLeader)
                    goalVec = Vector3.ClampMagnitude(_carrot.position - transform.position, 1);
                break;
        }

        RaycastHit hit;
        Vector3 origin = transform.position;
        Vector3 fwd = transform.forward;
        Vector3 right = transform.right;
        float rayDist = _rb.velocity.magnitude * 0.3f + 0.2f;
        const float fMul = 6; //How strong should we push in the opposite direction when detecting a collision
        const float tMul = 10; //How strong should we turn away from the collision

        if(debug) { //Draw all the sensors
            Debug.DrawRay(origin, fwd * rayDist, Color.red); //Front
            Debug.DrawRay(origin, -fwd * 0.1f, Color.red); //Back
            Debug.DrawRay(origin, (fwd + right * .5f) * rayDist, Color.red); //Diag Right
            Debug.DrawRay(origin, (fwd - right * .5f) * rayDist, Color.red); //Diag Left
            Debug.DrawRay(origin, right * .8f * rayDist, Color.red); //Right
            Debug.DrawRay(origin, -right * .8f * rayDist, Color.red); //Left
        }

        //Send out raycasts in the different directions, in order of which ones should be checked first
        if(Physics.Raycast(origin, -fwd, out hit, 0.1f, layerMask)) { //Back
            int turnRand = (int) Time.time % 6 < 3 ? 1 : -1; //Pick a direction "randomly" and switch every three seconds
            goalVec = fwd + turnRand * right;

            if(debug) Debug.DrawRay(origin, -fwd * 0.1f, Color.yellow); //Back - touching
        } else if(Physics.Raycast(origin, fwd + right * .5f, out hit, rayDist * .8f, layerMask)) { //Diag Right
            Vector3 diff = transform.position - hit.point;
            diff *= Vector3.Dot(diff, -fwd);
            float distMult = Lerp(fMul, fMul / 4, diff.sqrMagnitude); //Push away harder when closer
            float turnMult = Lerp(tMul / 3, tMul, diff.sqrMagnitude); //Turn more when farther
            Vector3 turnVec = Vector3.Cross(transform.up, hit.normal); //Turn to be parallel to the wall
            Vector3 newGoalVec = hit.normal * distMult + turnVec * turnMult;
            if(Vector3.Dot(newGoalVec, fwd) < -.5f) //If this is taking the bot too far from the goal
                newGoalVec += goalVec; //add the goal back in

            if(debug) {
                Debug.DrawRay(origin, (fwd + right * .5f) * rayDist, Color.yellow); //Diag Right - touching
                Debug.DrawRay(origin, diff, Color.cyan);
                Debug.DrawRay(origin, turnVec, Color.blue);
            }

            return newGoalVec;
        } else if(Physics.Raycast(origin, fwd - right * .5f, out hit, rayDist * .8f, layerMask)) { //Diag Left
            Vector3 diff = transform.position - hit.point;
            diff *= Vector3.Dot(diff, -fwd);
            float distMult = Lerp(fMul, fMul / 4, diff.sqrMagnitude); //Push away harder when closer
            float turnMult = Lerp(tMul / 3, tMul, diff.sqrMagnitude); //Turn more when farther
            Vector3 turnVec = Vector3.Cross(-transform.up, hit.normal); //Turn to be parallel to the wall
            Vector3 newGoalVec = hit.normal * distMult + turnVec * turnMult;
            if(Vector3.Dot(newGoalVec, fwd) < -.5f) //If this is taking the bot too far from the goal
                newGoalVec += goalVec; //add the goal back in

            if(debug) {
                Debug.DrawRay(origin, (fwd - right * .5f) * rayDist, Color.yellow); //Diag Left - touching
                Debug.DrawRay(origin, diff, Color.cyan);
                Debug.DrawRay(origin, turnVec, Color.blue);
            }

            return newGoalVec;
        } else if(Physics.Raycast(origin, right, out hit, rayDist, layerMask)) { //Right
            float turnMult = Vector3.Dot(hit.normal, -fwd);
            turnMult = turnMult > 0 ? turnMult : 0;
            goalVec += fwd * 0.5f - right * turnMult; //Keep going towards the goal, but don't turn into the wall

            if(debug) Debug.DrawRay(origin, right * .8f * rayDist, Color.yellow); //Right - touching
        } else if(Physics.Raycast(origin, -right, out hit, rayDist, layerMask)) { //Left
            float turnMult = Vector3.Dot(hit.normal, -fwd);
            turnMult = turnMult > 0 ? turnMult : 0;
            goalVec += fwd * 0.5f + right * turnMult; //Keep going towards the goal, but don't turn into the wall

            if(debug) Debug.DrawRay(origin, -right * .8f * rayDist, Color.yellow); //Left - touching
        } else if(Physics.Raycast(origin, fwd, out hit, rayDist, layerMask)) { //Front
            Vector3 diff = transform.position - hit.point;
            diff *= Vector3.Dot(diff, -fwd);
            float distMult = Lerp(fMul, .1f, diff.sqrMagnitude); //Push away harder when closer
            int turnRand = (int) Time.time % 6 < 3 ? 1 : -1; //Pick a direction "randomly" and switch every three seconds
            float turnMult = Lerp(.1f, tMul, diff.sqrMagnitude); //Turn more when farther
            Vector3 turnVec = Vector3.Cross(transform.up, diff.normalized);
            goalVec = hit.normal * distMult + turnVec * turnMult * turnRand;

            if(!debug) return goalVec;
            Debug.DrawRay(origin, fwd * rayDist, Color.yellow); //Front - touching
            Debug.DrawRay(origin, diff, Color.cyan);
        }
        //else nothing is touching

        return goalVec;
    }

    /// <summary> Sets bot movement based on a vector, where vector magnitude controls bot speed. </summary>
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

    /// <summary> Controls the bot movement. Should only be called once per physics timestep (last call will always override). </summary>
    /// <param name="fwdInput">Float between -1 and 1 that controls bots forward movement</param>
    /// <param name="turnInput">Float between -1 and 1 specifying how fast to turn. 1 is full right, -1 full left.</param>
    private void Move(float fwdInput, float turnInput) {
        foreach(WheelCollider wheel in wheelsR)
            wheel.motorTorque = fwdInput * speed - turnInput * turnSpeed;
        foreach(WheelCollider wheel in wheelsL)
            wheel.motorTorque = fwdInput * speed + turnInput * turnSpeed;
    }

    /// <summary> Checks if this bot is touching the ground with its wheels </summary>
    /// <returns>True if any of its wheels are touching the ground, false otherwise</returns>
    private bool IsGrounded() {
        foreach(WheelCollider wheel in wheelsR)
            if(wheel.isGrounded)
                return true;
        foreach(WheelCollider wheel in wheelsL)
            if(wheel.isGrounded)
                return true;
        return false;
    }
}