using UnityEngine;
using ExtensionMethods;

public class LauncheeScript : MonoBehaviour {
    #region Variables

    public PlayerDrone script;
    public float aimSpeed;
    public float push = 90;
    public bool gravity = true;
    public float size = 0.2957171f;
    public float t = 3;
    public Transform barrelT;
    private float _t2;
    private bool _b;
    public bool locked = true;
    private Vector3 _sizeVec;

    #endregion

    private void Start() {
        _sizeVec = new Vector3(size, size, size);
    }

    private void Update() {
        if(locked) {
            transform.localScale = Extensions.SharpInDamp(transform.localScale, _sizeVec, aimSpeed);
            transform.localPosition = barrelT.position;
            locked &= Vector3.Distance(_sizeVec, transform.localScale) >= 0.02f;
        } else {
            gameObject.GetComponent<Rigidbody>().useGravity &= gravity;
            if(!_b) {
                GetComponent<Rigidbody>().velocity = script.gameObject.GetComponent<Rigidbody>().velocity;
                transform.localPosition = barrelT.position;
                GetComponent<Rigidbody>().AddForce(barrelT.transform.up * push * GetComponent<Rigidbody>().mass);
                _t2 = t + 1;
                _b = true;
            }

            t -= Time.deltaTime;
            Vector3 vv = Vector3.zero;
            if(t < 0) {
                transform.localScale = Vector3.SmoothDamp(transform.localScale, Vector3.zero, ref vv, 0.1f);
            }

            Destroy(gameObject, _t2);
        }
    }
}