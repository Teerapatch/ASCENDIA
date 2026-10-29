using UnityEngine;
[RequireComponent(typeof(Cloth)), DisallowMultipleComponent]
public class CapeClothMotionReset : MonoBehaviour
{
    private Cloth cloth;
    private Vector3 previousPosition, previousScale;
    private void OnEnable() {
        cloth=GetComponent<Cloth>();
        previousPosition=transform.position; previousScale=transform.lossyScale;
    }
    private void LateUpdate() {
        if (!cloth) return;
        if ((transform.position-previousPosition).sqrMagnitude>4f ||
            (transform.lossyScale-previousScale).sqrMagnitude>0.00001f)
            cloth.ClearTransformMotion();
        previousPosition=transform.position; previousScale=transform.lossyScale;
    }
}
