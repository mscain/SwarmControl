using UnityEngine;
using ExtensionMethods;

public class MouseLookTestHelper : MonoBehaviour {

	public MouseLook script;
	public float aimSpeed;
	public float push = 90;
	public float hop = 20;
	public float maxForce = 20;
	public bool  gravity = true;
	public bool  clamped = true;
	public GameObject hitObj;
	public float size = 0.2957171f;
	public float t = 3;
	public Transform barrelT;
	float t2;
	bool  b;
	public bool locked = true;
	Vector3 sizeVec;
	void Start(){
		sizeVec = new Vector3(size, size, size);
	}
	void  Update(){
		if(locked){
			transform.localScale = Extensions.SharpInDamp(transform.localScale, sizeVec, aimSpeed);
			transform.localPosition = barrelT.position;
			locked &= Vector3.Distance(sizeVec, transform.localScale) >= 0.02f;
		} else{
			gameObject.GetComponent<Rigidbody>().useGravity &= gravity;
			if(!b){
				GetComponent<Rigidbody>().velocity = script.gameObject.GetComponent<Rigidbody>().velocity;
				transform.localPosition = barrelT.position;
				GetComponent<Rigidbody>().AddForce(barrelT.transform.up * push * GetComponent<Rigidbody>().mass);
				t2 = t + 1;
				b = true;
			}
			t -= Time.deltaTime;
			Vector3 vv = Vector3.zero;
			if(t < 0){
				transform.localScale = Vector3.SmoothDamp(transform.localScale, Vector3.zero, ref vv, 0.1f);
			}
			Destroy(gameObject, t2);
		}
	}
}