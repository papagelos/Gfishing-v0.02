using UnityEngine;
using UnityEngine.EventSystems;

public sealed class FarmMapClickCatcher : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Camera farmMapCamera;
    [SerializeField] private Grid grid; // the GameObject that has the Grid component

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!farmMapCamera || !grid) return;

        // Assumes your sprites/tilemaps are on Z = 0 and camera is at Z = -10 (or similar).
        float depth = -farmMapCamera.transform.position.z;

        Vector3 world = farmMapCamera.ScreenToWorldPoint(
            new Vector3(eventData.position.x, eventData.position.y, depth)
        );

        Vector3Int cell = grid.WorldToCell(world);

        Debug.Log($"[FarmMap] Click screen={eventData.position} world={world} cell={cell}");
    }
}
