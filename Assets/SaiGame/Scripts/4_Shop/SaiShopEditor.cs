using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(SaiShop))]
    [CanEditMultipleObjects]
    public class SaiShopEditor : Editor
    {
        private SaiShop saiShop;
        private SerializedProperty autoLoadOnLogin;
        private SerializedProperty shopLimit;
        private SerializedProperty shopOffset;

        private bool showCurrentShops = true;
        private bool showShopList = true;
        private bool showUtilityButtons = true;

        // Per-shop items cache: shopId → response
        private readonly Dictionary<string, ShopItemsResponse> shopItemsCache = new Dictionary<string, ShopItemsResponse>();
        // Per-shop items foldout state
        private readonly Dictionary<string, bool> shopItemsFoldout = new Dictionary<string, bool>();
        // Per-shop loading state
        private readonly HashSet<string> loadingShops = new HashSet<string>();

        private void OnEnable()
        {
            this.saiShop = (SaiShop)target;
            this.autoLoadOnLogin = serializedObject.FindProperty("autoLoadOnLogin");
            this.shopLimit = serializedObject.FindProperty("shopLimit");
            this.shopOffset = serializedObject.FindProperty("shopOffset");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Shop Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(this.autoLoadOnLogin, new GUIContent("Auto Load on Login", "Automatically load shops when user logs in"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Pagination Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(this.shopLimit, new GUIContent("Shop Limit", "Number of shops to load per request"));
            EditorGUILayout.PropertyField(this.shopOffset, new GUIContent("Shop Offset", "Offset for pagination"));

            EditorGUILayout.Space();

            // Current Shop Data
            this.showCurrentShops = EditorGUILayout.Foldout(this.showCurrentShops, "Current Shop Data", true);
            if (this.showCurrentShops)
            {
                EditorGUI.indentLevel++;

                if (this.saiShop.CurrentShopResponse != null)
                {
                    EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Total Shops: {this.saiShop.CurrentShopResponse.total}");
                    EditorGUILayout.LabelField($"Loaded Shops: {this.saiShop.CurrentShopResponse.shops?.Length ?? 0}");
                    EditorGUILayout.LabelField($"Limit: {this.saiShop.CurrentShopResponse.limit}  |  Offset: {this.saiShop.CurrentShopResponse.offset}");

                    if (this.saiShop.CurrentShopResponse.shops != null
                        && this.saiShop.CurrentShopResponse.shops.Length > 0)
                    {
                        this.showShopList = EditorGUILayout.Foldout(this.showShopList, $"Shop List ({this.saiShop.CurrentShopResponse.shops.Length})", true);
                        if (this.showShopList)
                        {
                            EditorGUI.indentLevel++;
                            foreach (ShopData shop in this.saiShop.CurrentShopResponse.shops)
                                this.DrawShopSummary(shop);
                            EditorGUI.indentLevel--;
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No shop data loaded yet.", MessageType.None);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Utility Buttons
            this.showUtilityButtons = EditorGUILayout.Foldout(this.showUtilityButtons, "Utility Actions", true);
            if (this.showUtilityButtons)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = Color.cyan;
                if (GUILayout.Button("Get Shops", GUILayout.Height(30)))
                    this.LoadShops();
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Clear Shops", GUILayout.Height(30)))
                {
                    this.saiShop.ClearShops();
                    this.shopItemsCache.Clear();
                    this.shopItemsFoldout.Clear();
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Event Listeners", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Events are automatically registered/unregistered with SaiAuth login/logout events.", MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawShopSummary(ShopData shop)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField($"Name: {shop.name}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"ID: {shop.id}");
            EditorGUILayout.LabelField($"Key: {shop.shop_key}");
            EditorGUILayout.LabelField($"Type: {shop.shop_type}  |  Active: {shop.is_active}");
            EditorGUILayout.LabelField($"Items: {shop.item_count}");

            if (!string.IsNullOrEmpty(shop.description))
                EditorGUILayout.LabelField($"Description: {shop.description}");

            if (!string.IsNullOrEmpty(shop.starts_at))
                EditorGUILayout.LabelField($"Starts: {shop.starts_at}");

            if (!string.IsNullOrEmpty(shop.ends_at))
                EditorGUILayout.LabelField($"Ends: {shop.ends_at}");

            EditorGUILayout.LabelField($"Created: {shop.created_at}");

            EditorGUILayout.Space(4);

            // Items button
            bool isLoading = this.loadingShops.Contains(shop.id);
            bool hasCached = this.shopItemsCache.ContainsKey(shop.id);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = isLoading ? Color.gray : new Color(0.4f, 1f, 0.6f);
            EditorGUI.BeginDisabledGroup(isLoading);
            if (GUILayout.Button(isLoading ? "Loading..." : "Items", GUILayout.Height(24)))
                this.LoadShopItemsForShop(shop.id);
            EditorGUI.EndDisabledGroup();
            GUI.backgroundColor = Color.white;

            if (hasCached)
            {
                GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                if (GUILayout.Button("Clear", GUILayout.Height(24), GUILayout.Width(50)))
                {
                    this.shopItemsCache.Remove(shop.id);
                    this.shopItemsFoldout.Remove(shop.id);
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();

            // Inline items display
            if (hasCached)
            {
                ShopItemsResponse cached = this.shopItemsCache[shop.id];

                if (!this.shopItemsFoldout.ContainsKey(shop.id))
                    this.shopItemsFoldout[shop.id] = true;

                this.shopItemsFoldout[shop.id] = EditorGUILayout.Foldout(
                    this.shopItemsFoldout[shop.id],
                    $"Items ({cached.items?.Length ?? 0})",
                    true);

                if (this.shopItemsFoldout[shop.id] && cached.items != null)
                {
                    EditorGUI.indentLevel++;
                    foreach (ShopItemData item in cached.items)
                        this.DrawShopItemSummary(item);
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawShopItemSummary(ShopItemData item)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField($"{item.display_name}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"ID: {item.id}");
            EditorGUILayout.LabelField($"Price: {item.price}  |  Stock: {item.stock}  |  Active: {item.is_active}");
            EditorGUILayout.LabelField($"Limit: {item.purchase_limit_type}  ({item.purchase_limit})  |  Restock: {item.restock_schedule}");

            if (!string.IsNullOrEmpty(item.description))
                EditorGUILayout.LabelField($"Description: {item.description}");

            if (!string.IsNullOrEmpty(item.available_from))
                EditorGUILayout.LabelField($"From: {item.available_from}");

            if (!string.IsNullOrEmpty(item.available_until))
                EditorGUILayout.LabelField($"Until: {item.available_until}");

            EditorGUILayout.EndVertical();
        }

        private void LoadShops()
        {
            if (SaiService.Instance == null)
            {
                Debug.LogError("[ShopEditor] SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                Debug.LogError("[ShopEditor] Not authenticated! Please login first.");
                return;
            }

            this.saiShop.GetShops(
                onSuccess: response =>
                {
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.Log($"[ShopEditor] Loaded {response.shops.Length} shops (total: {response.total})");
                    Repaint();
                },
                onError: error =>
                {
                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.LogError($"[ShopEditor] Failed to load shops: {error}");
                }
            );
        }

        private void LoadShopItemsForShop(string shopId)
        {
            if (SaiService.Instance == null)
            {
                Debug.LogError("[ShopEditor] SaiService not found!");
                return;
            }

            if (!SaiService.Instance.IsAuthenticated)
            {
                Debug.LogError("[ShopEditor] Not authenticated! Please login first.");
                return;
            }

            this.loadingShops.Add(shopId);
            Repaint();

            this.saiShop.GetShopItems(
                shopId: shopId,
                onSuccess: response =>
                {
                    this.loadingShops.Remove(shopId);
                    this.shopItemsCache[shopId] = response;
                    this.shopItemsFoldout[shopId] = true;

                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.Log($"[ShopEditor] Loaded {response.items?.Length ?? 0} items for shop {shopId}");
                    Repaint();
                },
                onError: error =>
                {
                    this.loadingShops.Remove(shopId);

                    if (SaiService.Instance == null || SaiService.Instance.ShowDebug)
                        Debug.LogError($"[ShopEditor] Failed to load items for shop {shopId}: {error}");
                    Repaint();
                }
            );
        }
    }
}
