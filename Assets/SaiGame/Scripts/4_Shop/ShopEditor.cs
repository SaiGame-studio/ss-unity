using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SaiGame.Services
{
    [CustomEditor(typeof(Shop))]
    [CanEditMultipleObjects]
    public class ShopEditor : Editor
    {
        private Shop shop;
        private SerializedProperty autoLoadOnLogin;
        private SerializedProperty autoRefreshAfterPurchase;
        private SerializedProperty shopLimit;
        private SerializedProperty shopOffset;

        private bool showCurrentShops = true;
        private bool showShopList = true;
        private bool showUtilityButtons = true;

        // Per-shop items cache: shopId → response
        private readonly Dictionary<string, ShopItemsResponse> shopItemsCache = new Dictionary<string, ShopItemsResponse>();
        // Per-shop items foldout state
        private readonly Dictionary<string, bool> shopItemsFoldout = new Dictionary<string, bool>();
        // Per-shop collapse state (keyed by shop id)
        private readonly Dictionary<string, bool> expandedShops = new Dictionary<string, bool>();
        // Per-shop loading state
        private readonly HashSet<string> loadingShops = new HashSet<string>();

        // Per-item purchase state
        private readonly Dictionary<string, int> itemQuantities = new Dictionary<string, int>();
        private readonly Dictionary<string, string> itemIdempotencyKeys = new Dictionary<string, string>();
        private readonly Dictionary<string, bool> itemAutoRandomKey = new Dictionary<string, bool>();
        private readonly HashSet<string> purchasingItems = new HashSet<string>();

        private void OnEnable()
        {
            this.shop = (Shop)target;
            this.autoLoadOnLogin = serializedObject.FindProperty("autoLoadOnLogin");
            this.autoRefreshAfterPurchase = serializedObject.FindProperty("autoRefreshAfterPurchase");
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
            EditorGUILayout.PropertyField(this.autoRefreshAfterPurchase, new GUIContent("Auto Refresh After Purchase", "Automatically reload shop items after a successful purchase"));

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

                if (this.shop.CurrentShopResponse != null)
                {
                    EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Total Shops: {this.shop.CurrentShopResponse.total}");
                    EditorGUILayout.LabelField($"Loaded Shops: {this.shop.CurrentShopResponse.shops?.Length ?? 0}");
                    EditorGUILayout.LabelField($"Limit: {this.shop.CurrentShopResponse.limit}  |  Offset: {this.shop.CurrentShopResponse.offset}");

                    if (this.shop.CurrentShopResponse.shops != null
                        && this.shop.CurrentShopResponse.shops.Length > 0)
                    {
                        this.showShopList = EditorGUILayout.Foldout(this.showShopList, $"Shop List ({this.shop.CurrentShopResponse.shops.Length})", true);
                        if (this.showShopList)
                        {
                            EditorGUI.indentLevel++;
                            foreach (ShopData shop in this.shop.CurrentShopResponse.shops)
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
                    this.shop.ClearShops();
                    this.shopItemsCache.Clear();
                    this.shopItemsFoldout.Clear();
                    this.expandedShops.Clear();
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

            // === COLLAPSIBLE HEADER ===
            string shopId = shop.id;
            if (!this.expandedShops.ContainsKey(shopId))
                this.expandedShops[shopId] = false;

            string headerLabel = $"★ {shop.name}  [{shop.item_count} items]";

            GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout);
            foldoutStyle.fontSize = 13;
            foldoutStyle.fontStyle = FontStyle.Bold;

            EditorGUILayout.BeginHorizontal();
            this.expandedShops[shopId] = EditorGUILayout.Foldout(this.expandedShops[shopId], headerLabel, true, foldoutStyle);

            // Active/Inactive badge (right-aligned)
            GUIStyle activeStyle = new GUIStyle(EditorStyles.label);
            activeStyle.fontSize = 11;
            activeStyle.normal.textColor = shop.is_active ? new Color(0.3f, 1f, 0.5f) : new Color(0.7f, 0.7f, 0.7f);
            activeStyle.fontStyle = FontStyle.Bold;
            activeStyle.alignment = TextAnchor.MiddleRight;
            EditorGUILayout.LabelField(shop.is_active ? "ACTIVE" : "INACTIVE", activeStyle, GUILayout.MinWidth(70));
            EditorGUILayout.EndHorizontal();

            if (!this.expandedShops[shopId])
            {
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"ID: {shop.id}");
            if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = shop.id;
            EditorGUILayout.EndHorizontal();
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
            if (GUILayout.Button(isLoading ? "Loading..." : "Get Items", GUILayout.Height(24)))
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
                        this.DrawShopItemSummary(item, shop.id);
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawShopItemSummary(ShopItemData item, string shopId)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField($"{item.display_name}", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"ID: {item.id}");
            if (GUILayout.Button("Copy", GUILayout.Width(50))) GUIUtility.systemCopyBuffer = item.id;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField($"Price: {item.price}  |  Stock: {item.stock}  |  Active: {item.is_active}");
            this.DrawPurchaseLimitInfo(item);

            if (!string.IsNullOrEmpty(item.description))
                EditorGUILayout.LabelField($"Description: {item.description}");

            if (!string.IsNullOrEmpty(item.available_from))
                EditorGUILayout.LabelField($"From: {item.available_from}");

            if (!string.IsNullOrEmpty(item.available_until))
                EditorGUILayout.LabelField($"Until: {item.available_until}");

            // ── Purchase UI ───────────────────────────────────────────────────────
            EditorGUILayout.Space(4);

            // Init state
            if (!this.itemQuantities.ContainsKey(item.id))
                this.itemQuantities[item.id] = 1;
            if (!this.itemAutoRandomKey.ContainsKey(item.id))
                this.itemAutoRandomKey[item.id] = true;
            if (!this.itemIdempotencyKeys.ContainsKey(item.id))
                this.itemIdempotencyKeys[item.id] = string.Empty;

            EditorGUILayout.BeginHorizontal();

            // Left column — 3 input rows
            EditorGUILayout.BeginVertical();

            this.itemQuantities[item.id] = EditorGUILayout.IntField("Quantity", this.itemQuantities[item.id]);
            if (this.itemQuantities[item.id] < 1)
                this.itemQuantities[item.id] = 1;

            EditorGUI.BeginDisabledGroup(this.itemAutoRandomKey[item.id]);
            this.itemIdempotencyKeys[item.id] = EditorGUILayout.TextField("Idempotency Key", this.itemIdempotencyKeys[item.id]);
            EditorGUI.EndDisabledGroup();

            this.itemAutoRandomKey[item.id] = EditorGUILayout.Toggle("Auto Key", this.itemAutoRandomKey[item.id]);

            EditorGUILayout.EndVertical();

            // Right column — Purchase button spanning all 3 rows
            bool isPurchasing = this.purchasingItems.Contains(item.id);
            GUI.backgroundColor = isPurchasing ? Color.gray : new Color(1f, 0.85f, 0f);
            EditorGUI.BeginDisabledGroup(isPurchasing);
            if (GUILayout.Button(isPurchasing ? "Purchasing..." : "Purchase", GUILayout.Width(100), GUILayout.ExpandHeight(true)))
            {
                string key = this.itemAutoRandomKey[item.id]
                    ? System.Guid.NewGuid().ToString()
                    : this.itemIdempotencyKeys[item.id];

                this.purchasingItems.Add(item.id);
                Repaint();

                this.shop.PurchaseItem(
                    shopId: shopId,
                    shopItemId: item.id,
                    quantity: this.itemQuantities[item.id],
                    idempotencyKey: key,
                    onSuccess: response =>
                    {
                        this.purchasingItems.Remove(item.id);
                        PurchaseRecord r = response.purchase_record;
                        Debug.Log(
                            $"[ShopEditor] <color=#FFD700><b>Purchase successful</b></color>\n" +
                            $"  id:                {r?.id}\n" +
                            $"  shop_item_id:      {r?.shop_item_id}\n" +
                            $"  user_id:           {r?.user_id}\n" +
                            $"  quantity:          {r?.quantity}\n" +
                            $"  unit_price:        {r?.unit_price}\n" +
                            $"  total_price:       {r?.total_price}\n" +
                            $"  idempotency_key:   {r?.idempotency_key}\n" +
                            $"  currency_item_def: {r?.currency_item_def_id}\n" +
                            $"  created_at:        {r?.created_at}");
                        if (this.shop.AutoRefreshAfterPurchase)
                            this.LoadShopItemsForShop(shopId);
                        Repaint();
                    },
                    onError: error =>
                    {
                        this.purchasingItems.Remove(item.id);
                        Debug.LogError($"[ShopEditor] Purchase failed for item {item.id}: {error}");
                        Repaint();
                    }
                );
            }
            EditorGUI.EndDisabledGroup();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawPurchaseLimitInfo(ShopItemData item)
        {
            GUIStyle style = new GUIStyle(EditorStyles.label);
            style.richText = true;
            style.fontStyle = FontStyle.Bold;
            style.fontSize = 11;

            string limitType = (item.purchase_limit_type ?? "unlimited").ToLower();
            string restockPart = $"  <color=#888888>Restock: {item.restock_schedule}</color>";

            if (limitType == "unlimited")
            {
                EditorGUILayout.LabelField(
                    $"<color=#00E676>∞  UNLIMITED</color>{restockPart}",
                    style);
            }
            else
            {
                // Scope colour: player = cyan, global = orange
                string scopeColor = limitType == "global" ? "#FF8C00" : "#66CCFF";
                string scopeLabel = limitType == "global" ? "GLOBAL" : "PLAYER";

                // Progress colour: green → yellow → red
                float ratio = item.purchase_limit > 0
                    ? (float)item.purchased_count / item.purchase_limit
                    : 0f;
                string progressColor = ratio >= 1f ? "#FF4444" :
                                       ratio >= 0.75f ? "#FFD700" : "#AAFFAA";

                string progressBar = $"{item.purchased_count} / {item.purchase_limit}";
                EditorGUILayout.LabelField(
                    $"<color={scopeColor}>{scopeLabel} LIMIT</color>  " +
                    $"<color={progressColor}><b>{progressBar}</b></color>" +
                    restockPart,
                    style);
            }
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

            this.shop.GetShops(
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

            this.shop.GetShopItems(
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
