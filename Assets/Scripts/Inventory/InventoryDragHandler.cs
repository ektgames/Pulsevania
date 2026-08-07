using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Pulsevania.Core
{
    public class InventoryDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private Canvas canvas;
        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private Vector3 startPosition;
        private Transform startParent;

        public int slotIndex = -1; // -1 means equipped slot
        public EquipSlot equippedSlotType = EquipSlot.None;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            canvas = GetComponentInParent<Canvas>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (InventoryManager.Instance == null || UIManager.Instance == null) return;

            ItemData item = null;
            if (slotIndex >= 0)
            {
                if (slotIndex < InventoryManager.Instance.inventoryItems.Count)
                {
                    item = InventoryManager.Instance.inventoryItems[slotIndex];
                }
            }
            else if (equippedSlotType != EquipSlot.None)
            {
                if (InventoryManager.Instance.equippedItems.ContainsKey(equippedSlotType))
                {
                    item = InventoryManager.Instance.equippedItems[equippedSlotType];
                }
            }

            if (item != null)
            {
                if (slotIndex >= 0 && item.equipSlot == EquipSlot.Consumable)
                {
                    InventoryManager.Instance.UseConsumable(slotIndex);
                    UIManager.Instance.ForceHideTooltip();
                }
                else
                {
                    // Single click/tap locks tooltip for mobile or PC reading
                    UIManager.Instance.LockTooltip(item, eventData.position);
                }
            }

            // Double tap/click to auto equip
            if (eventData.clickCount >= 2)
            {
                if (slotIndex >= 0)
                {
                    if (item != null)
                    {
                        if (item.equipSlot != EquipSlot.Consumable)
                        {
                            InventoryManager.Instance.EquipItem(item, slotIndex);
                        }
                        UIManager.Instance.ForceHideTooltip();
                    }
                }
                else if (equippedSlotType != EquipSlot.None)
                {
                    InventoryManager.Instance.UnequipItem(equippedSlotType);
                    UIManager.Instance.ForceHideTooltip();
                }
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ForceHideTooltip(); // Instantly hide and unlock tooltip during drag
            }

            startPosition = rectTransform.anchoredPosition;
            startParent = transform.parent;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0.6f;

            // Force center pivot so the dragged icon locks directly under the cursor/touch
            Vector2 localPosBefore = rectTransform.localPosition;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.localPosition = localPosBefore;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (canvas != null)
            {
                // Align touch/mouse precisely to the drag icon center
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    transform.parent as RectTransform, 
                    eventData.position, 
                    eventData.pressEventCamera, 
                    out Vector2 localPoint);
                rectTransform.anchoredPosition = localPoint;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;

            // Check what UI object is under the pointer
            GameObject hitObj = eventData.pointerCurrentRaycast.gameObject;
            bool success = false;

            if (hitObj != null)
            {
                // 1. Drag from inventory on the left to equipment slot on the right
                EquipmentSlotUI equipSlot = hitObj.GetComponentInParent<EquipmentSlotUI>();
                if (equipSlot != null && slotIndex >= 0)
                {
                    ItemData item = InventoryManager.Instance.inventoryItems[slotIndex];
                    if (item.equipSlot == equipSlot.targetSlot)
                    {
                        InventoryManager.Instance.EquipItem(item, slotIndex);
                        success = true;
                    }
                }

                // 2. Drag from equipped slot on the right to inventory panel on the left (Unequip)
                if (slotIndex == -1 && equippedSlotType != EquipSlot.None)
                {
                    bool droppedOnInventory = false;
                    Transform current = hitObj.transform;
                    while (current != null)
                    {
                        if (current.name.Contains("InventoryGrid") || current.name.Contains("InventoryPanel") || current.name.Contains("InventoryItemSlot") || current.name.Contains("SlotContainer"))
                        {
                            droppedOnInventory = true;
                            break;
                        }
                        current = current.parent;
                    }

                    if (droppedOnInventory)
                    {
                        InventoryManager.Instance.UnequipItem(equippedSlotType);
                        success = true;
                    }
                }
            }

            // Force visual alignment reset to prevent drifting icons
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.localPosition = Vector3.zero;

            if (!success)
            {
                // Snap back
                rectTransform.anchoredPosition = startPosition;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (InventoryManager.Instance == null || UIManager.Instance == null) return;

            ItemData item = null;
            if (slotIndex >= 0)
            {
                if (slotIndex < InventoryManager.Instance.inventoryItems.Count)
                {
                    item = InventoryManager.Instance.inventoryItems[slotIndex];
                }
            }
            else if (equippedSlotType != EquipSlot.None)
            {
                if (InventoryManager.Instance.equippedItems.ContainsKey(equippedSlotType))
                {
                    item = InventoryManager.Instance.equippedItems[equippedSlotType];
                }
            }

            if (item != null)
            {
                UIManager.Instance.ShowTooltip(item, eventData.position);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.HideTooltip();
            }
        }
    }
}
