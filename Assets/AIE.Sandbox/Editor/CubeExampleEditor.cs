using UnityEditor;
using UnityEngine;

namespace AIE.Sandbox.Editor
{
    [CustomEditor(typeof(AIE.Sandbox.CubeExample))]
    public class CubeExampleEditor : UnityEditor.Editor
    {
        float size = 1f;

        protected virtual void OnSceneGUI()
        {
            var myTarget = ((CubeExample)target);

            size = Handles.ScaleValueHandle(
                size,
                myTarget.transform.position + myTarget.transform.forward * size,
                myTarget.transform.rotation,
                HandleUtility.GetHandleSize(myTarget.transform.position) * 2f,
                Handles.CubeHandleCap,
                1f
            );

            Handles.DrawLine(
                myTarget.transform.position,
                myTarget.transform.position + myTarget.transform.forward * size
            );
        }
    }
}