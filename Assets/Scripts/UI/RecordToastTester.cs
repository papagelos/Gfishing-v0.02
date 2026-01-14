// Assets/Scripts/UI/RecordToastTester.cs
// Simple manual tester – press key to show a fake record toast.

using UnityEngine;

public sealed class RecordToastTester : MonoBehaviour
{
    [SerializeField] private RecordToastView toastView;

    [Header("Test Data")]
    [SerializeField] private string  testFishName  = "Fish_Bumblebee_Fish_0";
    [SerializeField] private float   testWeightKg  = 5.038f;
    [SerializeField] private float   testLengthCm  = 85.7f;
    [SerializeField] private int     testQuality   = 90;
    [SerializeField] private KeyCode triggerKey    = KeyCode.T;

    private void Update()
    {
        if (Input.GetKeyDown(triggerKey))
        {
            Debug.Log($"[RecordToastTester] Trigger key {triggerKey} pressed");

            if (toastView != null)
            {
                toastView.ShowNewRecord(
                    testFishName,
                    testWeightKg,
                    testLengthCm,
                    testQuality
                );
            }
            else
            {
                Debug.LogWarning("[RecordToastTester] ToastView is null – wire it in the inspector.");
            }
        }
    }
}
