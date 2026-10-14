# 🛠️ README: โครงสร้างโค้ดและคู่มือการเชื่อมระบบ (Dev Integration Guide)

เอกสารสรุปสถาปัตยกรรมโค้ดฝั่ง **Core / Player / Climbing / Node Routing / Inventory / Camp** สำหรับใช้อ้างอิงตอนรวมโปรเจกต์ (Merge / Combine) และเชื่อมข้อมูลข้ามระบบ[cite: 1]

---

## ⚠️ 1. สำคัญมากก่อนรวมโค้ด: สคริปต์ที่ใช้จริง vs สคริปต์เก่า (ห้ามหยิบผิด!)

ในโฟลเดอร์ `Scripts` ปัจจุบันมีไฟล์เวอร์ชันเก่าติดอยู่ 3 จุด ตอนเอาเข้า Scene หรือเขียนโค้ดเชื่อม **ให้ใช้เฉพาะตัวที่เป็น Active เท่านั้น**[cite: 1]:

* **ระบบการปีนเขาและหัก Stamina:**
  * ✅ **ตัวที่ใช้จริง:** `Player/PlayerClimbController.cs` คู่กับ `Player/PlayerPenaltySystem.cs` (ทำงานร่วมกับ `PlayerStateManager.cs`)[cite: 1]
  * ❌ **ตัวเก่า (ไม่ต้องใช้):** `Player/ClimbingController.cs` (เป็นสคริปต์ต้นแบบที่รวมทุกอย่างไว้ในไฟล์เดียว)[cite: 1]
* **ระบบจัดเซ็ตอาวุธในแคมป์ (Loadout UI):**
  * ✅ **ตัวที่ใช้จริง:** `Camp/WeaponCarousel.cs` (มีแอนิเมชัน, เช็กสถานะ `isUnlocked`, และแสดงป้าย `P1`–`P4`)[cite: 1]
  * ❌ **ตัวเก่า (ไม่ต้องใช้):** `Camp/LoadoutManager.cs` (เป็นตัวต้นแบบ ไม่มีระบบกรองอาวุธที่ล็อกอยู่)[cite: 1]
* **คลาสข้อมูลอาวุธ (`WeaponData`):**
  * ✅ **ตัวที่ใช้จริง:** `Camp/Data/WeaponData.cs`[cite: 1]
  * ❌ **ตัวเก่า (ไม่ต้องใช้):** `Camp/WeaponData.cs` (โค้ดข้างในถูก Comment ปิดไว้ทั้งหมด ลบไฟล์นี้ทิ้งได้เลย)[cite: 1]

---

## 🔄 2. ศูนย์กลางข้อมูลข้ามฉาก (Data Persistence & Singletons)

ระบบทั้งหมดในเกมใช้ **ScriptableObject (`PlayerData`)** ร่วมกับ **Singleton Managers** เป็นตัวกลางในการอ่านและเขียนข้อมูลข้ามฉาก[cite: 1]:

1. **`PlayerData.cs` (`Core/PlayerData.cs` - ScriptableObject):**
   * เป็นศูนย์กลางเก็บค่าสถานะทุกอย่างของผู้เล่น[cite: 1]
   * สคริปต์ทุกตัวจะลากไฟล์ Asset `PlayerData` ตัวเดียวกันไปใส่ใน Inspector หรือดึงผ่าน `GameManager.Instance.playerData`[cite: 1]
2. **`GameManager.cs` (`Player/GameManager.cs` - Singleton):**
   * มีคำสั่ง `DontDestroyOnLoad(gameObject)` เพื่อให้อยู่ข้ามทุก Scene[cite: 1]
   * **ข้อควรระวังตอนเทสต์แยก Scene:** ใน `Awake()` ของ `GameManager` จะสั่ง `playerData.ResetData()` เสมอเมื่อเริ่มรันครั้งแรก (รีเซ็ต Stamina เต็ม, `gemStone = 0`, `weight = 0`, `piton = 3`, `currentFloor = 1`, `currentCampLevel = 1`, และล้าง `activeLoadout`)[cite: 1]
3. **Singleton ตัวอื่นๆ ที่เรียกใช้ได้ผ่าน `.Instance`:**
   * `InventoryManager.Instance` — จัดการการเพิ่มของเข้ากระเป๋าและคำนวณน้ำหนัก[cite: 1]
   * `NodeSelectionManager.Instance` — จัดการระบบแผนที่เลือกเส้นทาง (Node Routing)[cite: 1]
   * `RoomManager.Instance` — จัดการการสุ่มและสลับ Prefab ห้องภายใน Scene เดิม[cite: 1]

---

## 🔌 3. จุดเชื่อมระบบ (Integration Hooks) สำหรับเอาโค้ดมารวมกัน

เวลาเอาโค้ดระบบอื่น (เช่น ระบบ Battle, MiniBoss, หรือ UI อื่นๆ) มารวมกับโค้ดชุดนี้ ให้เชื่อมผ่านจุดต่อไปนี้[cite: 1]:

### จุดที่ 1: การเปลี่ยนฉากเข้าสู่โหมด Battle / MiniBoss / Camp / Rest
* **ไฟล์ที่ควบคุม:** `Node/NodeSelectionManager.cs` (ภายใน Coroutine `ConfirmNode(PathNode selectedNode)`) และ `Node/PathNode.cs`[cite: 1]
* **หลักการทำงาน:**
  1. เมื่อผู้เล่นคลิกเลือกโหนดและตัวละครปีนไปถึงจุด `climbTarget` จอจะเฟดมืดลง และบวกชั้นปัจจุบันขึ้น 1 ชั้น (`GameManager.Instance.playerData.currentFloor++`)[cite: 1]
  2. ระบบจะเรียก `TriggerNodeEncounter(selectedNode)` เพื่อแจกของทันทีในกรณีที่เป็นโหนด `Ore` (เรียก `InventoryManager.Instance.AddItem`) หรือโหนด `Rest` (บวก `playerData.stamina` ตามค่า `node.staminaRestore`)[cite: 1]
  3. **การตัดสินใจโหลด Scene ใหม่:** โค้ดเช็กตัวแปร `shouldLoadNewScene` โดยดูว่าใน Inspector ของ `PathNode` มีการพิมพ์ชื่อ `sceneToLoad` ไว้หรือไม่ และ `roomType` เป็น `Battle`, `MiniBoss`, `Rest`, หรือ `Camp` หรือไม่[cite: 1]
  4. **วิธีเชื่อมระบบ:**
     * **ถ้าแยก Scene (เช่น มี Scene สำหรับ Battle หรือ Camp โดยเฉพาะ):** ให้ไปที่ Prefab ของโหนดใน `NodeSpawner` แล้วพิมพ์ชื่อ Scene ลงในช่อง `sceneToLoad` ของสคริปต์ `PathNode` ระบบจะปลดล็อกเมาส์ (`Cursor.lockState = CursorLockMode.None`) แล้วสั่ง `SceneManager.LoadScene(nextScene)` ทันที[cite: 1]
     * **ถ้าไม่ได้แยก Scene (เล่นใน Scene เดิมแต่เปลี่ยนห้อง):** ให้ปล่อยช่อง `sceneToLoad` ว่างไว้ ระบบจะเรียก `RoomManager.Instance.LoadNewRoom()` เพื่อเสก Prefab ห้องจาก `climbRoomPrefabs` หรือ `battleRoomPrefabs` แทน[cite: 1]

### จุดที่ 2: การดึงข้อมูลอาวุธและค่าสถานะไปใช้ในระบบต่อสู้ (Battle System)
* **ไฟล์ที่เกี่ยวข้อง:** `Core/PlayerData.cs` และ `Camp/Data/WeaponData.cs`[cite: 1]
* **วิธีดึงข้อมูลอาวุธที่ผู้เล่นสวมใส่มาจากแคมป์:**
  * ดึงอาวุธทั้ง 4 ช่องได้จาก `GameManager.Instance.playerData.activeLoadout` (เป็น Array `WeaponData[4]` ช่องที่ไม่ได้ใส่จะเป็น `null`)[cite: 1]
  * อาวุธที่ถืออยู่ในมือปัจจุบัน เก็บไว้ที่ตัวแปร `playerData.currentWeaponInHand`[cite: 1]
  * ความทนทานอาวุธรวมของผู้เล่น ดูและหักลบได้ที่ `playerData.currentWeaponDUR` (ค่าสูงสุดคือ `playerData.maxWeaponDUR`)[cite: 1]
* **⚠️ สำคัญมากเวลาดึงค่าพลังโจมตีและความเร็วของอาวุธ:**
  * **ห้ามดึง** `weaponData.attackDamage` หรือ `weaponData.speed` ไปใช้ตรงๆ เพราะนั่นคือค่าเริ่มต้นตอน Lv.1[cite: 1]
  * **ให้เรียกใช้ฟังก์ชัน** `weaponData.GetCurrentDamage()` และ `weaponData.GetCurrentSpeed()` เสมอ เพื่อให้ค่าที่ตีบวกมาจากโรงตีเหล็ก (`ForgeManager`) มีผลในฉากต่อสู้[cite: 1]
  * ค่าอื่นๆ ที่ดึงจาก `WeaponData` ได้เลย: `weaponType` (`Dagger`, `LongSword`, `Bow`, `Spell`), `postureMultiplier`, `freeAimMultiplier`, และ `availableSkills` (`List<WeaponSkill>`)[cite: 1]

### จุดที่ 3: การให้รางวัลหลังจบการต่อสู้ (ดรอปแร่ / ชิ้นส่วนมอนสเตอร์)
* **ไฟล์ที่เกี่ยวข้อง:** `Inventory/InventoryManager.cs` และ `Core/PlayerData.cs`[cite: 1]
* **วิธีเพิ่มไอเทมเข้ากระเป๋าพร้อมคิดน้ำหนัก:**
  * เรียก `InventoryManager.Instance.AddItem(ItemData itemToAdd, int amount)` ซึ่งจะคืนค่า `bool` (`true` = เก็บสำเร็จ, `false` = น้ำหนักเกิน `playerData.maxWeight`)[cite: 1]
  * **กฎสำคัญของการตั้งชื่อ `ItemData`:** ทุกครั้งที่เรียก `AddItem` ฟังก์ชัน `UpdatePlayerData()` ใน `InventoryManager` จะคำนวณ `playerData.weight` ใหม่ทั้งหมด และจะนับจำนวน `gemStone` กับ `monsterPart` จากชื่อไอเทม (`itemName`) โดยตรง[cite: 1]:
    * ถ้าอยากให้ไอเทมถูกนับเป็น **แร่ (`gemStone`)**: ชื่อ `itemName` ในไฟล์ Asset ต้องมีคำว่า `"Ore"` หรือ `"Gem"`[cite: 1]
    * ถ้าอยากให้ไอเทมถูกนับเป็น **ชิ้นส่วนมอนสเตอร์ (`monsterPart`)**: ชื่อ `itemName` ในไฟล์ Asset ต้องมีคำว่า `"Monster"`[cite: 1]

### จุดที่ 4: การจบแคมป์ (Camp Progression) เพื่อปลดล็อกโควตาอาวุธ
* **ไฟล์ที่เกี่ยวข้อง:** `Core/PlayerData.cs`, `Camp/ForgeManager.cs`, `Camp/WeaponCarousel.cs`[cite: 1]
* **หลักการทำงาน:**
  * ระบบแคมป์ใช้ตัวแปร `playerData.currentCampLevel` (เริ่มต้นที่ `1`) เป็นตัวกำหนดโควตาการคราฟต์อาวุธใหม่ใน `ForgeManager` และจำนวนช่องใส่อาวุธที่ปลดล็อกใน `WeaponCarousel`[cite: 1]
  * เมื่อผู้เล่นผ่านแคมป์เพื่อไปชั้นถัดไป ให้เรียกฟังก์ชัน `playerData.ReachNextCamp()` เพื่อบวก `currentCampLevel++` และปลดล็อกช่องอาวุธเพิ่ม (สูงสุด 4 ช่อง)[cite: 1]

---

## 📂 4. สรุปหน้าที่ของแต่ละโฟลเดอร์ (Module Breakdown)

### 4.1 โฟลเดอร์ `Player/` (การควบคุมตัวละครและสถานะ)
* **`PlayerStateManager.cs`:** คุม State Machine หลัก มี 5 สถานะ: `Walking`, `Climbing`, `Penalty`, `NodeSelection`, `AutoClimbing`[cite: 1]
* **`PlayerWalkController.cs`:** ใช้ New Input System (`InputActionReference moveAction`) เดินบนพื้นราบ และยิง Raycast ระยะ `wallDetectDistance` (`0.8f`) หากชนวัตถุที่มี Tag `"Wall"` จะเปลี่ยน State เป็น `Climbing` และสลับกล้องเป็น `climbCam`[cite: 1]
* **`PlayerClimbController.cs`:** ทำงานเมื่อ `currentState == State.Climbing` คำนวณลดความเร็วการปีนตามน้ำหนักของในกระเป๋า (`minSpeedMultiplier = 0.4f`) เมื่อปีนสูงถึง `maxPlayerHeight` (`5f`) จะหยุดขยับตัวละครขึ้นแต่เลื่อน `environmentContainer` ลงแทน[cite: 1]
* **`PlayerPenaltySystem.cs`:** หัก Stamina ขณะกดปุ่มปีนด้วยสูตร `(data.baseStaminaDrain + (data.weight * 0.1f)) * Time.deltaTime` เมื่อ Stamina หมดจะเข้าสู่สถานะ `Penalty` เฟดจอมืด หัก Piton 1 ชิ้น (`GameManager.Instance.UsePiton()`) และรีเซ็ต Stamina เต็ม แต่ถ้า `piton <= 0` จะสั่ง Game Over และส่งกลับไปสถานะ `Walking`[cite: 1]
* **`PlayerCameraController.cs`:** สลับกล้องระหว่าง `walkCam` กับ `climbCam` และคำนวณการเอียงกล้องตามเมาส์ (Camera Sway) ผ่าน `lookTarget`[cite: 1]

### 4.2 โฟลเดอร์ `Map/` (การสร้างฉาก ห้อง และไอเทมตามด่าน)
* **`RoomManager.cs`:** เสกห้องจาก `climbRoomPrefabs` หรือ `battleRoomPrefabs` ลงใน `roomContainer` โดยถ้าอยู่ชั้นที่ 1 จะเกิดที่ `PlayerSpawnPoint` แต่ถ้าชั้นมากกว่า 1 ในห้อง `Normal` จะย้ายไปเกิดบนกำแพง (`WallSpawnPoint`) และสั่งปิด `StartFloor`[cite: 1]
* **`MapGenerator.cs`:** เสกแผ่นหน้าผา `mountainPrefabs` ต่อกันขึ้นไปตามแกน Y ทีละ `tileHeight` (`20f`) จนครบ `totalTilesPerRoom` (`10` ชิ้น) โดยชิ้นสุดท้ายจะใช้ `topMountainPrefab`[cite: 1]
* **`OreSpawner.cs`:** สุ่มเสกไอเทมจากลิสต์ `spawnableItems` ตามโอกาส `spawnChance` (`0.4f`) ตามตำแหน่งลูก แล้วจับผูกเป็นลูกของ `"EnvironmentContainer"`[cite: 1]
* **`CollectibleItem.cs`:** แสดง `promptUI` เมื่อผู้เล่นเข้าใกล้และรับปุ่ม `E` (`Keyboard.current.eKey`) หากตั้ง `useImmediately = true` จะบวก Stamina ทันที แต่ถ้าเป็น `false` จะเก็บเข้า `InventoryManager`[cite: 1]
* **`ClimbingHold.cs`:** เช็กระยะห่างจากผู้เล่น ถ้าน้อยกว่า `grabDistance` (`1.5f`) แล้วกด `Space` จะดึงตำแหน่งผู้เล่นมาเกาะที่จุดยึด[cite: 1]

### 4.3 โฟลเดอร์ `Node/` (ระบบแผนที่และเลือกเส้นทาง)
* **`PathNode.cs`:** กำหนดประเภทโหนด (`Normal`, `Battle`, `MiniBoss`, `Ore`, `Rest`, `Camp`), ชื่อฉาก `sceneToLoad`, รางวัล (`oreRewardItem`, `oreAmount`, `staminaRestore`), และจุดปีนไปหา (`climbTarget`)[cite: 1]
* **`NodeSpawner.cs`:** สุ่มเสกโหนด 3 จุด (`leftPoint`, `centerPoint`, `rightPoint`) สำหรับชั้น 1–9 (การันตี `Normal` 1, `Battle` 1, และสุ่มโหนดที่ 3 จาก `Normal / Ore / Rest`) ส่วนชั้นที่ 10 ขึ้นไป (`currentFloor >= maxFloorBeforeCamp`) จะบังคับเสก `Rest`, `Camp`, และ `MiniBoss`[cite: 1]
* **`NodeSelectionTrigger.cs`:** เมื่อผู้เล่นชน Trigger จะเรียก `NodeSelectionManager.Instance.StartSelection()`[cite: 1]
* **`NodeSelectionManager.cs`:** สร้าง UI แผนที่และเส้นเชื่อมโหนด 2D อัตโนมัติ, ตรวจจับเมาส์ชี้/คลิกโหนด 3D, สั่งผู้เล่นปีนอัตโนมัติ (`AutoClimbing`) ไปยัง `climbTarget`, บวกชั้น, และโหลดห้อง/Scene ถัดไป[cite: 1]

### 4.4 โฟลเดอร์ `Inventory/` และ `Core/` (กระเป๋าและ UI HUD)
* **`ClimbUIManager.cs`:** กดปุ่ม `Tab` เพื่อเปิด/ปิดหน้าต่างกระเป๋า (`inventoryPanel`) และสลับกล้องไปที่ `inventoryCamera` (บล็อกไม่ให้เปิดหากหน้าจอเลือกโหนดเปิดอยู่)[cite: 1]
* **`InventoryUISlot.cs`:** แสดงไอคอนและจำนวนไอเทมในแต่ละช่อง พร้อมแสดง Tooltip เมื่อเมาส์ชี้[cite: 1]
* **`StatusBarUI.cs`:** อัปเดตหลอด `staminaBar` และ `weightBar` (`displayStamina + displayWeight`) พร้อมเอฟเฟกต์เด้งขยาย (`bounceScale`)[cite: 1]
* **`PitonUIController.cs`:** แสดงไอคอนจำนวนสมอ (`pitonIcons`) และเล่นแอนิเมชันเมื่อเสีย Piton[cite: 1]
* **`Billboard.cs`:** หมุนป้าย UI 3D เข้าหากล้องหลักตลอดเวลา และทำลายตัวเองเมื่อไอเทมแม่ถูกเก็บ[cite: 1]

### 4.5 โฟลเดอร์ `Camp/` (ระบบแคมป์ โรงตีเหล็ก และจัดอาวุธ)
* **`CampManager.cs`:** สลับหน้าต่าง `mainMenuPanel`, `loadoutPanel`, `forgePanel` และมีปุ่ม `OnClickRest()` สำหรับเติม `stamina` และ `currentWeaponDUR` จนเต็ม[cite: 1]
* **`CampMenuEffects.cs`:** ขยับกล้อง `CinemachineCamera` ตามเมาส์ และเพิ่มเอฟเฟกต์ขยายขนาดปุ่มเมื่อเมาส์ชี้ให้ทุกปุ่มในฉาก[cite: 1]
* **`ForgeManager.cs`:** ระบบคราฟต์และอัปเกรดอาวุธ (จำกัดโควตาการคราฟต์อาวุธใหม่ตาม `currentCampLevel` และตีบวกได้สูงสุด `maxLevel = 5`) เมื่อคราฟต์เสร็จจะใส่อาวุธลงช่องว่างใน `activeLoadout` อัตโนมัติ[cite: 1]
* **`WeaponCarousel.cs`:** จัดการช่องสวมใส่อาวุธ 4 สล็อต แสดงสถานะล็อกตามเลเวลแคมป์ แสดงสีออร่าตามอาวุธ และป้ายกำกับ `P1`–`P4`[cite: 1]

---

## ⚖️ 5. ตารางจุดปรับค่าสมดุลเกม (Balancing Guide)

| สิ่งที่ต้องการปรับแก้ | ไฟล์ / สคริปต์ที่ต้องไปปรับ | ชื่อตัวแปรใน Inspector | ค่าเริ่มต้น |
| :--- | :--- | :--- | :--- |
| **Stamina สูงสุด & อัตราลดพื้นฐาน** | ไฟล์ Asset `PlayerData` (`Core/PlayerData.cs`) | `maxStamina`, `baseStaminaDrain` | `100`, `10`[cite: 1] |
| **น้ำหนักกระเป๋าสูงสุด** | ไฟล์ Asset `PlayerData` (`Core/PlayerData.cs`) | `maxWeight` | `30`[cite: 1] |
| **จำนวนสมอ (ชีวิต) เริ่มต้น** | ไฟล์ Asset `PlayerData` (`Core/PlayerData.cs`) | `piton` (และใน `ResetData()`) | `3`[cite: 1] |
| **จำนวนชั้นก่อนถึงแคมป์** | ไฟล์ Asset `PlayerData` (`Core/PlayerData.cs`) | `maxFloorBeforeCamp` | `10`[cite: 1] |
| **ความเร็วเดิน & ระยะจับกำแพง** | ผู้เล่น (`PlayerWalkController.cs`) | `walkSpeed`, `wallDetectDistance` | `5`, `0.8`[cite: 1] |
| **ความเร็วปีน (ขึ้น-ลง / ซ้าย-ขวา)** | ผู้เล่น (`PlayerClimbController.cs`) | `verticalSpeed`, `horizontalSpeed` | `5`, `3`[cite: 1] |
| **ความเร็วต่ำสุดเมื่อของเต็มกระเป๋า** | ผู้เล่น (`PlayerClimbController.cs`) | `minSpeedMultiplier` | `0.4` (40%)[cite: 1] |
| **โอกาสเกิดแร่บนหน้าผา** | จุดเสกแร่ (`Map/OreSpawner.cs`) | `spawnChance` | `0.4` (40%)[cite: 1] |
| **Stamina ที่ได้จากโหนด Rest** | Prefab โหนด (`Node/PathNode.cs`) | `staminaRestore` | `50`[cite: 1] |
| **จำนวนแร่ที่ได้จากโหนด Ore** | Prefab โหนด (`Node/PathNode.cs`) | `oreRewardItem`, `oreAmount` | `20`[cite: 1] |
| **น้ำหนักไอเทมแต่ละชิ้น** | ไฟล์ Asset ใน `Data/` (`ItemData.cs`) | `weightPerItem`, `maxStack` | แร่ `1`, ยา `0.5`[cite: 1] |
| **ค่าพลังอาวุธ & อัตราเติบโตต่อเลเวล** | ไฟล์ Asset ใน `Camp/Data/` (`WeaponData.cs`) | `attackDamage`, `speed`, `attackIncreasePerLevel`, `speedIncreasePerLevel` | ATK `+5`, Speed `-2`[cite: 1] |

---

## ✅ 6. Checklist การตั้งค่าใน Unity ตอนเอา Scene มารวมกัน

1. **ตั้งค่า Active Input Handling เป็น `Both`:**
   * ไปที่ `Edit > Project Settings > Player > Other Settings > Active Input Handling` แล้วเลือกเป็น **`Both`** เพราะ `PlayerWalkController` และ `CollectibleItem` ใช้ระบบ `UnityEngine.InputSystem` ตัวใหม่ แต่สคริปต์ปีนเขาและ UI ตัวอื่นๆ ใช้ `UnityEngine.Input` ตัวเก่า[cite: 1]
2. **ตั้งค่า Tag ให้ครบ:**
   * ตัวละครผู้เล่นต้องตั้ง Tag เป็น **`Player`**[cite: 1]
   * กำแพงที่ใช้เดินชนเพื่อเริ่มปีนต้องตั้ง Tag เป็น **`Wall`**[cite: 1]
3. **ชื่อ GameObject ใน Scene ที่โค้ดใช้ `Find()` ค้นหาอัตโนมัติ (ห้ามตั้งชื่อผิด):**
   * ในฉากปีนเขา: ต้องมี **`EnvironmentContainer`**[cite: 1]
   * ใน Prefab ห้องของ `RoomManager`: ต้องมีลูกชื่อ **`PlayerSpawnPoint`**, **`WallSpawnPoint`**, และ **`StartFloor`**[cite: 1]
   * ในระบบเลือกโหนด (`NodeSelectionManager`): หากไม่ได้ลากใส่ Inspector โค้ดจะค้นหาตามชื่อ **`MapUIPanel`**, **`NodeCam`**, **`FadeImage`**, **`UINodeContainer`**, **`UILineContainer`**, และ **`PlayerUIIcon`**[cite: 1]
   * ใน Prefab ย่อยของระบบ UI/Node: ใน `PathNode` ต้องมีลูกชื่อ **`Aura3D`**, ใน `uiNodePrefab` ต้องมี **`Aura`** และ **`Icon`**, ใน `blueprintPrefab` ต้องมี **`Icon`**, และใน `weaponGridPrefab` ต้องมี **`Icon`**, **`EquippedGlow`**, และ **`SlotNumText`**[cite: 1]