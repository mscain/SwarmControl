using UnityEngine;
using System.Collections;
using System.IO;

public class EndExperiment : MonoBehaviour {

	#if UNITY_ANDROID
	private bool newTouch = false;
	private float touchTime;
	private bool longPressDetected = false;
	#endif

	// Update is called once per frame
	void Update()
	{
		// Check for the escape key being pressed
		#if UNITY_STANDALONE || UNITY_EDITOR

		if (Input.GetButtonDown ("Cancel")) {
//			Debug.Log ("Escape: Quit!");
			EndExperimentCleanup ();
		}
		#elif UNITY_ANDROID
		if (Input.touchCount == 1){    
			Touch touch = Input.GetTouch(0);
			if(touch.phase==TouchPhase.Began){
				newTouch = true;
				touchTime = Time.time;
			}else if (touch.phase == TouchPhase.Stationary){
				if(newTouch==true && Time.time-touchTime>1f){
					newTouch = false;
					longPressDetected = true;
				}else if (newTouch == false && longPressDetected==false){ // began not detected
					newTouch = true;
					touchTime = Time.time;
				}
			}else{
				newTouch = false;
				longPressDetected = false;
			}
		}

		if (longPressDetected==true) {
			//Application.LoadLevel("ExperimentLoader");
			EndExperimentCleanup ();
		}
		#endif
	}
	
	public void EndExperimentCleanup()
	{
		#if UNITY_STANDALONE
		Application.Quit();
		#endif
	
		#if UNITY_EDITOR
		UnityEditor.EditorApplication.ExecuteMenuItem("Edit/Play");
		#endif
	}
}
