# ASCENDIA — UI Screen Inventory & Wireframes

**ขอบเขต:** `Assets/_kin/Scenes/CampScene.unity` และ `Assets/_kin/Scenes/ClimbScene.unity`  
**สำหรับ:** ทีม UI/UX และ Visual Design  
**สถานะเอกสาร:** สรุปจาก Scene, scripts และ prefabs ที่อยู่ใน `_kin`; ใช้เป็น brief สำหรับออกแบบต่อ ไม่ใช่ข้อกำหนดภาพสุดท้าย

## 1. ภาพรวมและสถานะ

| ID | หน้าจอ / state | Scene | สถานะจากโปรเจกต์ |
|---|---|---|---|
| C-01 | Camp Main / Base Camp | CampScene | มี `MainMenuPanel`; มีปุ่ม Forge, Loadout, Rest และข้อความสถานะแคมป์ |
| C-02 | Loadout | CampScene | มี `LoadoutPanel`; มี 4 ช่องอาวุธและสถานะล็อกตามระดับแคมป์ |
| C-03 | Weapon Selection | CampScene | มี `SelectionPanel`; เป็น overlay เลือก/ถอด/ย้ายอาวุธที่ปลดล็อกแล้ว |
| C-04 | Forge | CampScene | มี `ForgePanel`; มีรายการพิมพ์เขียว, รายละเอียด, ค่าวัตถุดิบ และปุ่ม Craft/Upgrade |
| C-05 | Camp feedback / transition | CampScene | มี `SystemMessageText` สำหรับข้อความสั้นและเปลี่ยนเข้า ClimbScene หลังพัก |
| L-01 | Climb HUD | ClimbScene | มี stamina/weight bars, piton icons และ fade overlay |
| L-02 | Route / Node Selection Map | ClimbScene | มี `MapUIPanel`; สร้าง node, เส้นทาง และตำแหน่งผู้เล่นแบบ runtime |
| L-03 | Inventory | ClimbScene | มี `InventoryPanel`; เปิด/ปิดด้วย Tab, แสดง 15 ช่อง |
| L-04 | Item Tooltip | ClimbScene | มี `TooltipPanel`; แสดงชื่อและคำอธิบายเมื่อชี้ไอเทม |
| L-05 | World Interaction Prompts | ClimbScene | มี prompt บน collectible / จุดเกาะใน prefab; เป็น world-space ไม่ใช่ Canvas screen เต็ม |
| L-06 | Stamina penalty / Piton lost feedback | ClimbScene | มี fade และ animation ของ piton; ยังไม่มีรายละเอียดข้อความ/วิธีแก้สถานการณ์บนจอ |
| L-07 | Game Over / Run End | ClimbScene | **ควรออกแบบเพิ่ม**; โค้ดตรวจกรณี Piton หมดและย้ายตัวละคร แต่ไม่มี end screen ที่ผู้เล่นใช้ตัดสินใจต่อได้ |
| L-08 | Pause / Settings | ClimbScene | **ข้อเสนอเพิ่ม**; ไม่พบ pause/settings panel ใน scene/scripts ของ `_kin` |

### หลักการวางกรอบ

- ใช้จอ gameplay ที่ยังเห็นฉากหลังเป็นฐานของ Camp และ Climb; ใช้ panel/overlay เฉพาะงานที่ต้องเลือกหรืออ่านรายละเอียด
- ให้ระดับแคมป์, ชั้น, stamina, น้ำหนัก, Piton และทรัพยากรสำคัญอ่านได้เร็วและสม่ำเสมอ
- แยกสถานะ **พร้อมทำ / ทำไม่ได้ / ล็อก / กำลังประมวลผล / สำเร็จ / ล้มเหลว** ด้วยทั้งสี ไอคอน และข้อความ ไม่พึ่งสีอย่างเดียว
- ออกแบบ layout ให้รองรับจอ 16:9 และ safe area; ตรวจที่ความละเอียดต่ำและ UI scale ต่างกัน
- ปุ่มยืนยันการพักและออกจาก run ควรระบุผลลัพธ์ก่อนกด เพราะเปลี่ยน stage หรือจบ run

## 2. CampScene

### C-01 — Camp Main / Base Camp

**วัตถุประสงค์:** หน้าหลักสำหรับเลือกจัดอาวุธ, เข้าโรงตีเหล็ก หรือพักแล้วเริ่มช่วงปีนถัดไป  
**องค์ประกอบจากโค้ด:** ปุ่ม `Btn_Loadout`, `Btn_Forge`, `Btn_Rest`; ข้อความ `BASE CAMP`; ข้อความต้อนรับ/เตือน final camp ใน `SystemMessageText`.

```text
+----------------------------------------------------------------+
| ASCENDIA                                        CAMP 2 / 4     |
|                                                                |
|                    [ภาพฉากค่าย / ตัวละคร]                      |
|                                                                |
|   [ LOADOUT ]       [ FORGE ]       [ REST & CLIMB ]           |
|                                                                |
|   STAMINA  ████████████ 100/100     WEAPON DUR  █████ 100/100  |
|   ORE 12   MONSTER PART 4            PITON 3                   |
|                                                                |
|   สถานะ: Base Camp — เตรียมตัวก่อนออกเดินทาง                   |
+----------------------------------------------------------------+
```

**พฤติกรรม/ข้อมูล:** แสดงระดับ `currentCampLevel / finalCampLevel`; สรุป stamina, durability อาวุธ, Ore/Gem, Monster Part และ Piton เท่าที่ข้อมูลจริงมีในระบบ. ปุ่ม Rest เติม stamina และ durability, เพิ่มระดับแคมป์/รีเซ็ตชั้น แล้วโหลด `ClimbScene`. ถ้าเป็น final camp ให้แทนข้อความปกติด้วยคำเตือนว่าเส้นทางถัดไปมุ่งสู่ Boss.

**ออกแบบให้ต่อ:** แนะนำ confirm state ของ Rest: “พักและเริ่ม Stage N” / “ออกเดินทางสู่ Final Climb”; แสดงผลที่จะเกิดขึ้นและปุ่มยืนยัน/กลับ. ตรวจสอบว่า `SystemMessageText` ที่ใช้ปัจจุบันเหมาะกับข้อความสถานะชั่วคราว ไม่ใช่ modal ยืนยัน.

### C-02 — Loadout

**วัตถุประสงค์:** ตรวจและจัดช่องอาวุธที่จะนำติดตัวไปปีน  
**องค์ประกอบจากโค้ด:** 4 slots `Slot0–Slot3`, icon, ชื่อ, lock icon และ aura; ช่องที่ใช้ได้ขึ้นกับระดับแคมป์ (`currentCampLevel`).

```text
+----------------------------------------------------------------+
| < BACK                         LOADOUT                         |
|  ช่องที่ปลดล็อก: 2 / 4                                         |
|                                                                |
|   [ P1  ไอคอนอาวุธ ]  ชื่อ / Lv. / ประเภท      [เลือกช่อง]    |
|   [ P2  ว่าง       ]  เลือกอาวุธ                 [เลือกช่อง]    |
|   [ P3  🔒         ]  ปลดล็อกที่ Camp Lv.3                     |
|   [ P4  🔒         ]  ปลดล็อกที่ Camp Lv.4                     |
|                                                                |
|   เลือกช่องเพื่อเปิดรายการอาวุธที่ผูกแล้ว                       |
|                                      [กลับสู่ค่าย]              |
+----------------------------------------------------------------+
```

**พฤติกรรม/ข้อมูล:** แตะช่องที่ปลดล็อกเพื่อเปิด C-03; แสดงชื่อ/ไอคอน/สีประจำอาวุธ. ช่องล็อกต้องแสดงระดับที่ต้องการและตอบสนองว่าเหตุใดกดไม่ได้. กำหนดสถานะว่างให้ชัดเจน. ระบุผลของการเลือกอาวุธที่อยู่ช่องอื่นว่าจะย้ายช่อง (ตามระบบปัจจุบัน) และสามารถถอดอาวุธได้.

### C-03 — Weapon Selection (overlay ใน Loadout)

**วัตถุประสงค์:** เลือกอาวุธที่คราฟต์แล้วเพื่อใส่หรือถอดจากช่องที่เลือก  
**องค์ประกอบจากโค้ด:** `SelectionPanel`; สร้างรายการจากอาวุธที่ `isUnlocked` เท่านั้น; แสดงแสง/หมายเลข P1–P4 ถ้ากำลังใช้อยู่.

```text
+-------------------------------- LOADOUT -----------------------+
| ช่อง P1: เลือกอาวุธ                                             |
|                                                                |
|  +----------------------+   +----------------------+           |
|  | [icon] Long Sword    |   | [icon] Bow       P2  |           |
|  | Lv. 2                |   | Lv. 1                |           |
|  +----------------------+   +----------------------+           |
|  +----------------------+   +----------------------+           |
|  | [icon] Dagger   P1   |   | [icon] Spell          |           |
|  | Lv. 1   [ติดตั้งแล้ว]|   | Lv. 1                |           |
|  +----------------------+   +----------------------+           |
|                 [ถอดออก]          [ปิด X]                      |
+----------------------------------------------------------------+
```

**พฤติกรรม/ข้อมูล:** เลือกอาวุธแล้วอัปเดต Loadout ทันที; ถ้าอาวุธอยู่ช่องอื่นให้ย้ายมา slot ปัจจุบัน; กดอาวุธเดิมอีกครั้ง/ปุ่มถอดออกเพื่อทำช่องว่าง. ปิด overlay แล้วกลับ Loadout. หากมีอาวุธจำนวนมากให้ grid เลื่อนได้. ปัจจุบันระบบไม่มีปุ่ม “ถอดออก” แยกชัดเจน จึงควรใส่ affordance ให้ค้นพบวิธีถอดได้.

### C-04 — Forge

**วัตถุประสงค์:** คราฟต์อาวุธใหม่หรืออัปเกรดอาวุธ พร้อมประเมินค่าใช้จ่ายก่อนทำรายการ  
**องค์ประกอบจากโค้ด:** `ForgePanel`, `ForgeGridContent`, `CraftingConsole`, ไอคอนวัสดุ, ชื่อ/คำอธิบาย, stats และ action button เดียว. สถานะปุ่มเปลี่ยนตามโควตาแคมป์, ระดับอาวุธ, เพดานเลเวลและวัตถุดิบ.

```text
+----------------------------------------------------------------+
| < BACK                    FORGE                  ORE 12 | PART 4|
|                                                                |
| พิมพ์เขียว            รายละเอียดอาวุธ       โต๊ะตีเหล็ก         |
| +----------+           Long Sword             [ภาพอาวุธ]        |
| | Dagger   |           คำอธิบายสั้น            Lv.2 -> Lv.3     |
| | LongSword|           ATK 20 -> 25                             |
| | Bow      |           Speed 4 -> 4.5      ค่าใช้จ่าย            |
| | Spell    |                               Ore       12 / 8       |
| +----------+                               Part       4 / 3       |
|                                           [ UPGRADE ]            |
|  สถานะ: มีวัตถุดิบพอ / ต้องการ Ore หรือ Part เพิ่ม              |
+----------------------------------------------------------------+
```

**พฤติกรรม/ข้อมูล:** รายการซ้าย; เลือกอาวุธแล้วรายละเอียด/ไอคอน/ค่าสถานะ/ราคาเปลี่ยน. แสดงค่าปัจจุบัน → ค่าหลังทำรายการ; วัตถุดิบที่มี / ต้องใช้; action label เป็น Craft Weapon, Upgrade, Max Level Reached หรือ Camp Limit Reached ตาม state. ปุ่ม disabled พร้อมคำอธิบายเมื่อขาดของ/โควตา. เมื่อสำเร็จแสดงผลลัพธ์ที่ชัดเจนและค่าใหม่.

**เนื้อหาที่ต้องตัดสินใจร่วมกับทีมระบบ:** ปัจจุบันการคราฟต์ผูกอาวุธเข้าช่องว่างแรกให้อัตโนมัติ. ออกแบบ success state ให้บอกช่องที่ถูกใช้; ถ้าไม่มีช่องว่าง ให้ระบุพฤติกรรมที่ระบบต้องรองรับก่อนส่ง UI final. ตรวจข้อความราคาให้ชัดว่า “มี / ต้องใช้” (ข้อมูลใน script ปัจจุบันเรียง `playerData / req`).

### C-05 — Camp feedback / transition

**รูปแบบ:** toast/banner บน C-01 หรือ C-04 ไม่จำเป็นต้องเป็นหน้าใหม่. แสดงผลคราฟต์/อัปเกรด, ช่องที่ถูกผูก, การพักสำเร็จ, Stage ใหม่ และข้อความ final climb. ใช้ fade/scale สั้นและไม่บังปุ่มหลัก; ข้อความสำคัญต้องค้างนานพออ่านได้ก่อนเปลี่ยน Scene.

## 3. ClimbScene

### L-01 — Climb HUD (เดิน/ปีน)

**วัตถุประสงค์:** แสดงการอยู่รอดและ resource ระหว่างปีนโดยไม่บังจุดเกาะ/เส้นทาง  
**องค์ประกอบจากโค้ด:** `StaminaBar`, `WeightBar`, `PitonContainer` (3 icons), `FadeImage`; status bars อัปเดตจาก PlayerData. weight bar ปัจจุบันแสดง stamina + weight เป็นแถบซ้อน/เปรียบเทียบ.

```text
+----------------------------------------------------------------+
| FLOOR 04                                      [⏸ PAUSE]        |
|                                                                |
|                                                                |
|                   [พื้นที่มองหน้าผา/ตัวละคร]                    |
|                                                                |
|   PITON  ● ● ○                                                  |
|   STAMINA  ███████░░░  72 / 100                                 |
|   LOAD     █████████░  86 / 100   (น้ำหนักมีผลต่อการปีน)       |
|                                                                |
|   [E] เก็บแร่         [Space] เกาะจุดปีน       [Tab] กระเป๋า    |
+----------------------------------------------------------------+
```

**ข้อมูล:** ชั้นปัจจุบัน; stamina / max; น้ำหนัก / maxWeight (แนะนำให้ UI แสดงน้ำหนักจริงพร้อมหน่วย แทนการให้ผู้เล่นเดาค่าจาก stamina + weight bar); จำนวน Piton ที่เหลือ. ให้เตือนสี/รูปแบบเมื่อ stamina ต่ำ, น้ำหนักใกล้เต็ม, Piton เหลือน้อย. แสดง control hint ตามสถานการณ์และ input mapping จริง. ปุ่ม Tab ใช้เปิดกระเป๋าได้ยกเว้นขณะ map selection เปิด.

### L-02 — Route / Node Selection Map

**วัตถุประสงค์:** ให้ผู้เล่นเลือกโหนดถัดไปบนแผนที่เส้นทาง  
**องค์ประกอบจากโค้ด:** `MapUIPanel`, `UINodeContainer`, `UILineContainer`, `PlayerUIIcon`; node ประเภท Normal, Battle, MiniBoss, Boss, Ore, Rest, Camp; hover แสดงชื่อและคลิกเลือก. แผนที่สร้างจาก node runtime.

```text
+----------------------------------------------------------------+
| เลือกเส้นทางถัดไป                          FLOOR 04            |
|                                                                |
|                                [BATTLE]                        |
|                               /         \                      |
|   [PLAYER ●] ----------------             [ORE]                 |
|                               \         /                      |
|                                [REST]                          |
|                                                                |
|  เลือก node เพื่อดูชื่อ/ชนิด/ผลที่คาดได้                        |
|  [ชื่อ node]  [คำอธิบายหรือรางวัล/การฟื้นฟู ถ้ามีข้อมูล]        |
|  คลิก node เพื่อยืนยันเส้นทาง                                   |
+----------------------------------------------------------------+
```

**พฤติกรรม/ข้อมูล:** player position และเส้นทางปัจจุบันต้องเด่น; เส้นทางที่เลือกเปลี่ยนสี/แสง; node อื่นถูกลดความเด่นหลังยืนยัน. ออกแบบ legend ของชนิด node และ hover/focus/selected state; ข้อความผลลัพธ์แสดงเฉพาะข้อมูลที่มี เช่น Ore reward หรือ Rest restore. เมื่อคลิกแล้วแสดงช่วงกำลังเดินทาง/ปีนอัตโนมัติ ก่อนปิด map และโหลดห้อง/scene ตามชนิด node.

**ช่องว่างจาก UI ปัจจุบัน:** โค้ดพบ hover และคลิกเลือก แต่ไม่มีแผงรายละเอียด node ที่สื่อผลลัพธ์ชัดในชื่อ GameObject ที่อ่านได้; ให้ดีไซน์พิจารณา info strip/tooltip ที่ไม่บังแผนที่. รองรับ keyboard/controller focus หากเกมต้องการนอกเหนือจากเมาส์.

### L-03 — Inventory

**วัตถุประสงค์:** ตรวจไอเทมที่เก็บระหว่างช่วงปีน  
**องค์ประกอบจากโค้ด:** `InventoryPanel`, `GridContent`, 15 visible slots, เปิด/ปิดด้วย Tab; กล้อง inventory แยก; ไม่อนุญาตให้เปิดขณะ map เปิด.

```text
+--------------------------- INVENTORY --------------------------+
| กระเป๋า                                      [ปิด  Tab / X]     |
| น้ำหนัก 18 / 30        Ore 12       Monster Part 4            |
|                                                                |
| [ไอคอน x3] [ไอคอน x2] [ว่าง] [ว่าง] [ว่าง]                    |
| [ไอคอน x1] [ว่าง]     [ว่าง] [ว่าง] [ว่าง]                    |
| [ว่าง]     [ว่าง]     [ว่าง] [ว่าง] [ว่าง]                    |
|                                                                |
| ชี้ที่ไอเทมเพื่อดูชื่อและคำอธิบาย                              |
+----------------------------------------------------------------+
```

**พฤติกรรม/ข้อมูล:** 15 ช่องคงที่; แสดง icon, stack count และสถานะช่องว่าง. แสดงน้ำหนักเทียบ maxWeight และจำนวนวัสดุที่ Forge ใช้. Tooltip ยึดกับ cursor/slot โดยไม่ออกนอกขอบจอ. เมื่อไอเทมถูกปฏิเสธเพราะน้ำหนักเกิน ให้มี toast ที่อ่านได้บน HUD; ปัจจุบันระบบส่ง warning ลง log เท่านั้น.

### L-04 — Item Tooltip

**รูปแบบ:** tooltip ขนาดเล็กตามตำแหน่งเมาส์; ชื่อ, ไอคอน (ถ้ามี), คำอธิบาย และข้อมูลที่มีจริง. จำกัดความกว้าง/ตัดบรรทัด; เลื่อนตำแหน่งอัตโนมัติเมื่อชิดขอบจอ. หายเมื่อ pointer ออกจาก slot หรือปิด Inventory. ไม่มี action เพิ่มที่ระบบยังไม่รองรับ.

### L-05 — World Interaction Prompts

**รูปแบบ:** prompt world-space ลอยใกล้วัตถุ; ใช้ icon ปุ่มตาม input device และคำกริยาสั้น เช่น `E เก็บ` สำหรับของเก็บ, `Space เกาะ` สำหรับ climbing hold. ปรากฏเมื่ออยู่ในระยะและหายเมื่อออกนอกระยะ/เก็บสำเร็จ. แยก visual สำหรับของที่เก็บทันที (เช่นฟื้น stamina) กับของที่ใส่กระเป๋า. prefab ปัจจุบันมี prompt UI สำหรับ collectible; จุดเกาะไม่ควรสรุปว่ามี screen prompt จนกว่าจะตรวจ scene runtime.

### L-06 — Stamina Penalty / Piton Lost

**วัตถุประสงค์:** สื่อว่าผู้เล่นหมด stamina, เสีย Piton และกลับปีนต่อพร้อม stamina ที่เติมใหม่  
**สิ่งที่มี:** fade overlay; ไอคอน Piton ลดสี/เล่น animation เมื่อเสีย.  
**Wireframe state:** ใช้ HUD overlay สั้น ไม่หยุด flow นานเกินจำเป็น.

```text
                    STAMINA หมด
             ใช้ Piton 1 อันเพื่อช่วยชีวิต
                    PITON 2 / 3
        stamina ฟื้นแล้ว — เตรียมปีนต่อ
```

**หมายเหตุ:** ข้อความด้านบนเป็นข้อเสนอด้าน presentation; logic ปัจจุบันหัก Piton และเติม stamina ก่อนกลับสู่ Climbing. ควรแสดง Piton ที่เหลือหลังหัก และลด motion/flash ที่อาจทำให้อ่านสถานะพลาด.

### L-07 — Game Over / Run End (ออกแบบเพิ่ม)

**เงื่อนไข:** Piton หมดหลัง penalty. ปัจจุบันโค้ด log “GAME OVER” และย้ายตัวละครกลับตำแหน่งเริ่มต้น แต่ไม่มีหน้าจอจบ run หรือ action ให้ผู้เล่น.  
**ข้อเสนอ:** หยุด input/gameplay และเปิด full-screen overlay ที่อ่านได้ทันที. ปุ่มที่มีได้ตามระบบเกมจริง เช่น Restart Run / Return to Camp / Main Menu; อย่าแสดงตัวเลือกที่ยังไม่มี navigation รองรับ.

```text
+----------------------------------------------------------------+
|                         RUN ENDED                               |
|                  Piton หมด — ไปต่อไม่ได้                        |
|                                                                |
|      ชั้นสูงสุด 04       แคมป์ 2       ไอเทมที่เก็บได้: ...      |
|                                                                |
|                [เริ่มการปีนใหม่]                                |
|                [กลับเมนูหลัก*]                                  |
+----------------------------------------------------------------+
*แสดงเฉพาะเมื่อมีปลายทางเมนูหลักเชื่อมจริง
```

### L-08 — Pause / Settings (ข้อเสนอเพิ่ม)

**เหตุผล:** ไม่พบ pause panel ใน ClimbScene scripts; มี gameplay ต่อเนื่องและ Inventory ใช้ Tab แต่ไม่มี pause/settings state ที่ระบุไว้. หากเป็น scope ของเกม ให้ดีไซน์เป็น overlay บนฉากปีน.

```text
+-------------------------- PAUSED ------------------------------+
|                         PAUSED                                 |
|                  [กลับไปปีนต่อ]                                |
|                  [การตั้งค่า]                                   |
|                  [ออกจากการปีน]                                |
+----------------------------------------------------------------+
```

ออกแบบ Settings เป็น subpanel เฉพาะเมื่อระบบรองรับจริง (เสียง, ภาพ, input). “ออกจากการปีน” ต้องมี confirmation และระบุผลต่อ run. ปุ่ม pause/input binding ต้องกำหนดร่วมกับทีมระบบ.

## 4. Flow ระหว่างหน้าจอ

```text
Camp Main (C-01)
  ├─ Loadout (C-02) ─ Weapon Selection (C-03) ─ กลับ Loadout
  ├─ Forge (C-04) ─ feedback ผลลัพธ์ (C-05) ─ กลับ Camp Main
  └─ Rest / ยืนยัน ─ ฟื้นสถานะ + เพิ่ม Camp Stage ─ ClimbScene

Climb HUD (L-01)
  ├─ เก็บ/เกาะ ─ world prompt (L-05) ─ inventory/resource feedback
  ├─ Tab ─ Inventory (L-03) ─ Tooltip (L-04) ─ กลับ HUD
  ├─ ถึงทางแยก ─ Route Map (L-02) ─ เลือก node ─ เดินทาง/โหลดต่อ
  ├─ Stamina หมด ─ Piton penalty feedback (L-06) ─ กลับปีนต่อ
  ├─ Piton หมด ─ Run End (L-07; ต้องออกแบบและเชื่อมระบบ)
  └─ Pause (L-08; ถ้าอยู่ใน scope และเชื่อม input/system)
```

## 5. รายการส่งต่อให้ทีมดีไซน์

1. ออกแบบ key art/layout และ component states ของ C-01 ถึง C-04, L-01 ถึง L-04.
2. กำหนด visual language แยกชนิด node, rarity/ประเภทอาวุธ, locked state และ resource shortage.
3. ทำ responsive/safe-area pass; ระบุขั้นต่ำขนาดตัวอักษร, hit area และลำดับ focus.
4. ทำ motion spec สำหรับ panel transition, selected node, item tooltip, resource update และ Piton loss โดยคำนึงถึง reduced motion.
5. ยืนยัน scope L-07 Game Over และ L-08 Pause/Settings กับทีมระบบ; ทำ final screen เฉพาะตัวเลือกที่ระบบนำทางได้จริง.
6. ก่อนลงงานจริง ให้ทีมระบบยืนยันข้อความ/ค่า runtime ของระดับแคมป์, ราคา, reward, น้ำหนัก, สถานะอาวุธ และ input binding; เอกสารนี้อิงค่าที่ scripts เปิดเผย ไม่กำหนด balance.

## 6. แหล่งอ้างอิงภายในโปรเจกต์

- Scenes: `Assets/_kin/Scenes/CampScene.unity`, `Assets/_kin/Scenes/ClimbScene.unity`
- Camp: `Scripts/Camp/CampManager.cs`, `WeaponCarousel.cs`, `ForgeManager.cs`
- Climb/map: `Scripts/Inventory/ClimbUIManager.cs`, `Scripts/Core/StatusBarUI.cs`, `PitonUIController.cs`, `Scripts/Node/NodeSelectionManager.cs`, `Scripts/Player/PlayerPenaltySystem.cs`
- Player state/data: `Scripts/Core/PlayerData.cs`, `Scripts/Player/GameManager.cs`
- Existing implementation notes: `Assets/_kin/Scripts/README.md`
