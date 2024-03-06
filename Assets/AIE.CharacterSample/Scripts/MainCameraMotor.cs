using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AIE.CharacterSample
{
    [ExecuteInEditMode]
    [DefaultExecutionOrder(100)]
    public class MainCameraMotor : MonoBehaviour
    {
        public Transform CameraToShadow;

        private void LateUpdate()
        {
            if(CameraToShadow == null) { return; }
            transform.position = CameraToShadow.position;
            transform.rotation = CameraToShadow.rotation;
        }
    }
}
