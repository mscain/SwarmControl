using UnityEngine;
using ExtensionMethods;

public class LauncheeScript : MonoBehaviour {
    #region Variables

    public PlayerDrone launcherScript;
    public float push = 90;
    public float size = 0.2957171f;
    public Transform barrelT;
    private float _t = 2;
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
            transform.localScale = Extensions.SharpInDamp(transform.localScale, _sizeVec, 1);
            transform.localPosition = barrelT.position;
            locked &= Vector3.Distance(_sizeVec, transform.localScale) >= 0.02f;
        } else {
            if(!_b) {
                GetComponent<Rigidbody>().velocity = launcherScript.GetComponent<Rigidbody>().velocity;
                transform.localPosition = barrelT.position;
                GetComponent<Rigidbody>().AddForce(barrelT.transform.up * push * GetComponent<Rigidbody>().mass);
                _t2 = _t + 1;
                _b = true;
            }

            _t -= Time.deltaTime;

            if(_t < 0)
                transform.localScale = Extensions.SharpInDamp(transform.localScale, Vector3.zero, 1f);

            Destroy(gameObject, _t2);
        }
    }
}