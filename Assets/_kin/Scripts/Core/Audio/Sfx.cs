// ชื่อเหตุการณ์เสียงทั้งหมด (id ใน SoundLibrary) ใช้ค่าคงที่นี้แทนการพิมพ์สตริงเอง
// เพิ่มเหตุการณ์ใหม่: ใส่ชื่อที่นี่ + เพิ่มแถวใน AscendiaBuildSoundLibrary.Defaults (หรือเพิ่มเองใน SoundLibrary.asset)
public static class Sfx
{
    // ---- UI
    public const string UIHover = "ui.hover";
    public const string UIClick = "ui.click";
    public const string UIConfirm = "ui.confirm";
    public const string UIBack = "ui.back";
    public const string UIError = "ui.error";

    // ---- อาวุธผู้เล่น
    public const string DaggerSwing = "weapon.dagger.swing";
    public const string SwordSwing = "weapon.sword.swing";
    public const string GunShot = "weapon.gun.shot";
    public const string SpellCast = "weapon.spell.cast";
    public const string KnifeThrow = "weapon.knife.throw";
    public const string WeaponSwitch = "weapon.switch";

    // ---- โดน / กระทบ
    public const string HitFlesh = "hit.flesh";
    public const string HitCritical = "hit.critical";
    public const string ShotImpact = "hit.shot";
    public const string BlastImpact = "hit.blast";
    public const string KnifeImpact = "hit.knife";
    public const string PlayerHurt = "player.hurt";

    // ---- เสียงพูด/ร้องของผู้เล่น
    public const string PlayerGrunt = "player.vocal.grunt";
    public const string PlayerAttackVocal = "player.vocal.attack";
    public const string PlayerDeath = "player.vocal.death";
    public const string PlayerWin = "player.vocal.win";

    // ---- Parry
    public const string ParrySuccess = "parry.success";
    public const string ParryHint = "parry.hint";

    // ---- ศัตรู
    public const string EnemyBladeAttack = "enemy.blade.attack";
    public const string EnemyAxeAttack = "enemy.axe.attack";
    public const string EnemyWandCast = "enemy.wand.cast";
    public const string EnemyBossAttack = "enemy.boss.attack";
    public const string EnemyBossRoar = "enemy.boss.roar";
    public const string EnemyHurt = "enemy.hurt";
    public const string EnemyDeath = "enemy.death";
    public const string EnemySpawn = "enemy.spawn";
    public const string EnemyStagger = "enemy.stagger";
    public const string EnemyVocalAttack = "enemy.vocal.attack";
    public const string EnemyVocalHeavy = "enemy.vocal.heavy";
    public const string EnemyVocalDeath = "enemy.vocal.death";

    // ---- การต่อสู้
    public const string TurnPlayer = "battle.turn.player";
    public const string TurnEnemy = "battle.turn.enemy";
    public const string Victory = "battle.victory";
    public const string Defeat = "battle.defeat";
    public const string QTEHit = "battle.qte.hit";
    public const string QTEMiss = "battle.qte.miss";
    public const string StatusBuff = "battle.status.buff";
    public const string StatusDebuff = "battle.status.debuff";
    public const string OrbHarvest = "battle.orb.harvest";

    // ---- ปีนเขา
    public const string ClimbLoop = "climb.loop";
    public const string WalkLoop = "climb.walk";
    public const string ClimbJump = "climb.jump";
    public const string ClimbLand = "climb.land";
    public const string Footstep = "climb.step";
    public const string FootstepRun = "climb.run";
    public const string PickupGem = "climb.pickup.gem";
    public const string PickupHerb = "climb.pickup.herb";
    public const string PitonLost = "climb.piton.lost";
    public const string FloorWhoosh = "climb.floor";

    // ---- แคมป์
    public const string ForgeHammer = "camp.forge.hammer";
    public const string ForgeUpgrade = "camp.forge.upgrade";
    public const string CampRest = "camp.rest";

    // ---- เพลง / บรรยากาศ (ใส่ไฟล์เพลงใน SoundLibrary ได้เลย)
    public const string MusicTitle = "music.title";
    public const string MusicClimb = "music.climb";
    public const string MusicCamp = "music.camp";
    public const string MusicBattle = "music.battle";
    public const string AmbWind = "amb.wind";
    public const string AmbCampfire = "amb.campfire";
    public const string AmbBattle = "amb.battle";
}
