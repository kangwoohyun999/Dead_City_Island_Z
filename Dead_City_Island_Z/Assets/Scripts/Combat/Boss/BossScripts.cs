using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// HospitalBoss, MilitaryBoss, BunkerBoss, Grenade, TimedDamageZone, PoisonPool (3D)

public class HospitalBoss : BossAI
{
    [SerializeField] private GameObject syringeProjectile, scalpelProjectile, poisonPoolPrefab, infectionFX, clonePrefab;
    [SerializeField] private float p1MeleeDmg=20f, p1SyringeDmg=15f, p1SyringeSpd=8f;
    [SerializeField] private float p2ScalpelDmg=25f; [SerializeField] private int p2ScalpelCount=5;
    [SerializeField] private float p2PoisonDur=6f, p3ExplosionDmg=60f, p3ExplosionRadius=4.5f, p3RageMult=1.6f;
    [SerializeField] private int p3CloneCount=3;
    private float _attackTimer;

    protected override IEnumerator OnBossStart() { StopMovement(); UIManager.Instance?.ShowNotification("💉 타락한 의사가 깨어났다..."); yield return new WaitForSeconds(1.5f); yield return FlashEffect(new Color(0.6f,1f,0.6f),0.2f,3); }

    protected override IEnumerator Phase1Loop()
    {
        _attackTimer = 0f;
        while (_currentPhase==1&&!_isDead)
        {
            _attackTimer -= Time.deltaTime;
            if (DistanceToPlayer()<=1.6f) { yield return Melee(p1MeleeDmg); _attackTimer=1.2f; }
            else if (_attackTimer<=0) { yield return Syringe(); _attackTimer=2.5f; }
            else MoveTowardPlayer();
            yield return null;
        }
    }

    protected override IEnumerator Phase2Loop()
    {
        _attackTimer=0f; float pt=5f;
        while (_currentPhase==2&&!_isDead)
        {
            _attackTimer-=Time.deltaTime; pt-=Time.deltaTime;
            if (pt<=0) { yield return SpawnPoison(); pt=8f; }
            else if (_attackTimer<=0) { if (DistanceToPlayer()<=1.6f) { yield return Melee(p1MeleeDmg*1.2f); _attackTimer=1f; } else { yield return FanScalpel(); _attackTimer=3f; } }
            else MoveTowardPlayer(1.2f);
            yield return null;
        }
    }

    protected override IEnumerator Phase3Loop()
    {
        yield return Explosion(); SpawnClones(); _attackTimer=0f; float et=12f;
        while (_currentPhase==3&&!_isDead)
        {
            _attackTimer-=Time.deltaTime; et-=Time.deltaTime;
            if (et<=0) { yield return Explosion(); et=12f; }
            else if (_attackTimer<=0) { if (DistanceToPlayer()<=1.6f) { yield return Melee(p1MeleeDmg*1.5f); yield return new WaitForSeconds(0.3f); yield return Melee(p1MeleeDmg*1.5f); _attackTimer=0.8f; } else { yield return FanScalpel(); _attackTimer=2f; } }
            else MoveTowardPlayer(p3RageMult);
            yield return null;
        }
    }

    private IEnumerator Melee(float dmg) { StopMovement(); _animator?.SetTrigger(AnimAttack); yield return new WaitForSeconds(0.25f); DealDamageInRadius(1.8f,dmg); yield return new WaitForSeconds(0.3f); }
    private IEnumerator Syringe() { StopMovement(); FacePlayer(); _animator?.SetTrigger(AnimAttack); yield return new WaitForSeconds(0.3f); FireProjectile(syringeProjectile,DirectionToPlayer(),p1SyringeSpd,p1SyringeDmg); yield return new WaitForSeconds(0.2f); }
    private IEnumerator FanScalpel()
    {
        StopMovement(); FacePlayer(); _animator?.SetTrigger(AnimAttack); yield return new WaitForSeconds(0.35f);
        Vector3 base3 = DirectionToPlayer(); float baseAng = Mathf.Atan2(base3.z,base3.x)*Mathf.Rad2Deg, spread = 40f;
        for (int i=0;i<p2ScalpelCount;i++) { float a=(baseAng-spread*0.5f+spread/(p2ScalpelCount-1)*i)*Mathf.Deg2Rad; FireProjectile(scalpelProjectile,new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),10f,p2ScalpelDmg); }
        yield return new WaitForSeconds(0.3f);
    }
    private IEnumerator SpawnPoison()
    {
        StopMovement(); yield return new WaitForSeconds(0.4f);
        if (poisonPoolPrefab!=null&&_player!=null) { Vector3 pos=new Vector3(_player.position.x,0f,_player.position.z); var go=Instantiate(poisonPoolPrefab,pos,Quaternion.identity); go.GetComponent<PoisonPool>()?.Init(8f,4f,p2PoisonDur); }
        yield return new WaitForSeconds(0.3f);
    }
    private IEnumerator Explosion() { StopMovement(); UIManager.Instance?.ShowNotification("☣️ 감염 폭발!"); yield return FlashEffect(new Color(0.4f,1f,0.4f),0.1f,6); if (infectionFX) Instantiate(infectionFX,transform.position,Quaternion.identity); DealDamageInRadius(p3ExplosionRadius,p3ExplosionDmg); CameraController.Instance?.Shake(0.8f,0.25f); yield return new WaitForSeconds(0.5f); }
    private void SpawnClones() { if (clonePrefab==null) return; for (int i=0;i<p3CloneCount;i++) { float a=i*(360f/p3CloneCount)*Mathf.Deg2Rad; Vector3 p=transform.position+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*3f; if (NavMesh.SamplePosition(p,out NavMeshHit h,3f,NavMesh.AllAreas)) p=h.position; Instantiate(clonePrefab,p,Quaternion.identity); } UIManager.Instance?.ShowNotification($"👥 분신 {p3CloneCount}체!"); }
}

public class PoisonPool : MonoBehaviour
{
    private float _dps,_radius,_duration,_elapsed;
    public void Init(float dps,float r,float dur) { _dps=dps;_radius=r;_duration=dur; transform.localScale=new Vector3(r*2f,0.05f,r*2f); }
    private void Update() { _elapsed+=Time.deltaTime; if (_elapsed>=_duration){Destroy(gameObject);return;} Collider[] hits=Physics.OverlapSphere(transform.position,_radius); foreach(var h in hits) if(h.TryGetComponent(out SurvivalStats s)) s.TakeDamage(_dps*Time.deltaTime,DamageType.Poison); }
}

public class MilitaryBoss : BossAI
{
    [SerializeField] private GameObject bulletPrefab,grenadePrefab,rocketPrefab,turretPrefab,shieldFX,nukeFX;
    [SerializeField] private float p1BulletDmg=12f,p1GrenDmg=45f,p1GrenRadius=3f; [SerializeField] private int p1BulletCount=8;
    [SerializeField] private float p2RocketDmg=70f,p2ShieldDur=4f; [SerializeField] private int p2TurretCount=2;
    [SerializeField] private float p3NukeDmg=120f,p3NukeRadius=8f,p3DashForce=14f;
    private bool _shieldActive; private float _shieldHealth;

    protected override IEnumerator OnBossStart() { StopMovement(); UIManager.Instance?.ShowNotification("🪖 대령 사체!"); yield return FlashEffect(new Color(0.8f,0.6f,0.2f),0.15f,4); yield return new WaitForSeconds(1f); }

    protected override IEnumerator Phase1Loop()
    {
        float gt=6f;
        while (_currentPhase==1&&!_isDead) { gt-=Time.deltaTime; if(gt<=0){yield return Grenade();gt=8f;} else if(DistanceToPlayer()>5f){MoveTowardPlayer();yield return null;} else {yield return Burst(p1BulletCount);} yield return new WaitForSeconds(0.5f); }
    }

    protected override IEnumerator Phase2Loop()
    {
        yield return Shield(); SpawnTurrets(); float rt=5f,st=p2ShieldDur,gt=10f;
        while (_currentPhase==2&&!_isDead) { rt-=Time.deltaTime;st-=Time.deltaTime;gt-=Time.deltaTime; if(st<=0&&_shieldActive)DeactivateShield(); if(!_shieldActive&&DistanceToPlayer()>6f)MoveTowardPlayer(1.1f); if(rt<=0){yield return Rocket();rt=5f;} if(gt<=0){yield return Grenade();gt=10f;} yield return null; }
    }

    protected override IEnumerator Phase3Loop()
    {
        UIManager.Instance?.ShowNotification("☢️ 핵 카운트다운!"); float nt=20f,rt=4f,dt=6f;
        while (_currentPhase==3&&!_isDead) { nt-=Time.deltaTime;rt-=Time.deltaTime;dt-=Time.deltaTime; if(nt>0&&nt<=5f)UIManager.Instance?.ShowNotification($"☢️ {Mathf.CeilToInt(nt)}초!"); if(nt<=0){yield return Nuke();nt=25f;} if(rt<=0){StopMovement();FireRadialProjectiles(bulletPrefab,12,10f,p1BulletDmg*1.5f);rt=4f;yield return new WaitForSeconds(0.5f);} if(dt<=0&&DistanceToPlayer()>2f){yield return Dash();dt=6f;} MoveTowardPlayer(1.4f);yield return null; }
    }

    private IEnumerator Burst(int shots) { StopMovement();FacePlayer(); for(int i=0;i<shots;i++){FireProjectile(bulletPrefab,Quaternion.AngleAxis(UnityEngine.Random.Range(-8f,8f),Vector3.up)*DirectionToPlayer(),14f,p1BulletDmg);yield return new WaitForSeconds(0.1f);} }
    private IEnumerator Grenade() { StopMovement();FacePlayer();_animator?.SetTrigger(AnimAttack);yield return new WaitForSeconds(0.4f); if(grenadePrefab!=null&&_player!=null){var go=Instantiate(grenadePrefab,transform.position+Vector3.up,Quaternion.identity);go.GetComponent<Grenade>()?.Init(_player.position,p1GrenDmg,p1GrenRadius);}yield return new WaitForSeconds(0.6f); }
    private IEnumerator Rocket() { StopMovement();FacePlayer();UIManager.Instance?.ShowNotification("🚀");yield return new WaitForSeconds(0.5f);FireProjectile(rocketPrefab,DirectionToPlayer(),12f,p2RocketDmg);yield return new WaitForSeconds(0.4f); }
    private void SpawnTurrets() { if(turretPrefab==null)return; for(int i=0;i<p2TurretCount;i++){float a=i*(180f/(p2TurretCount-1))*Mathf.Deg2Rad;Vector3 p=transform.position+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*3.5f;if(NavMesh.SamplePosition(p,out NavMeshHit h,3f,NavMesh.AllAreas))p=h.position;Instantiate(turretPrefab,p,Quaternion.identity);} }
    private IEnumerator Shield() { _shieldActive=true;_shieldHealth=200f;shieldFX?.SetActive(true);UIManager.Instance?.ShowNotification("🛡️");yield return new WaitForSeconds(0.3f); }
    private void DeactivateShield() { _shieldActive=false;shieldFX?.SetActive(false); }
    public override void TakeDamage(float damage,Vector3 hitPoint) { if(_shieldActive){_shieldHealth-=damage*0.5f;if(_shieldHealth<=0)DeactivateShield();return;}base.TakeDamage(damage,hitPoint); }
    private IEnumerator Nuke() { StopMovement();if(nukeFX)Instantiate(nukeFX,transform.position,Quaternion.identity);UIManager.Instance?.ShowNotification("☢️ 핵 폭발!");CameraController.Instance?.Shake(1.5f,0.4f);yield return new WaitForSeconds(0.8f);DealDamageInRadius(p3NukeRadius,p3NukeDmg);yield return new WaitForSeconds(0.5f); }
    private IEnumerator Dash()
    {
        if(_rb==null)yield break; StopMovement();
        _agent.enabled=false;_rb.isKinematic=false;
        Vector3 d=DirectionToPlayer();d.y=0.1f;_rb.AddForce(d.normalized*p3DashForce,ForceMode.Impulse);DealDamageInRadius(1.5f,p1BulletDmg*2f);
        yield return new WaitForSeconds(0.4f);_rb.linearVelocity=Vector3.zero;_rb.isKinematic=true;_agent.enabled=true;
    }
}

public class Grenade : MonoBehaviour
{
    private Vector3 _target,_start; private float _dmg,_radius,_elapsed,_dur=1.2f;
    [SerializeField] private GameObject explosionPrefab;
    public void Init(Vector3 t,float d,float r){_target=t;_dmg=d;_radius=r;_start=transform.position;}
    private void Update() { _elapsed+=Time.deltaTime;float t=_elapsed/_dur;if(t>=1f){Explode();return;}transform.position=Vector3.Lerp(_start,_target,t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*3f;transform.Rotate(Vector3.right,200f*Time.deltaTime); }
    private void Explode() { if(explosionPrefab)Instantiate(explosionPrefab,transform.position,Quaternion.identity);Collider[]hits=Physics.OverlapSphere(transform.position,_radius);foreach(var h in hits)if(h.TryGetComponent(out SurvivalStats s))s.TakeDamage(_dmg,DamageType.Physical);CameraController.Instance?.Shake(0.5f,0.15f);Destroy(gameObject); }
}

public class TimedDamageZone : MonoBehaviour
{
    private float _dmg,_radius,_dur,_elapsed;
    public void Init(float d,float r,float dur){_dmg=d;_radius=r;_dur=dur;}
    private void Update() { _elapsed+=Time.deltaTime;if(_elapsed>=_dur){Destroy(gameObject);return;}Collider[]hits=Physics.OverlapSphere(transform.position,_radius);foreach(var h in hits)if(h.TryGetComponent(out SurvivalStats s))s.TakeDamage(_dmg*Time.deltaTime,DamageType.Physical); }
}

public class BunkerBoss : BossAI
{
    [SerializeField] private GameObject spikePrefab,beamPrefab,magneticFX,copyPrefab;
    [SerializeField] private float p1SpikeDmg=25f,p2BeamDmg=8f,p3MagRadius=6f,p3MagForce=5f;

    protected override IEnumerator OnBossStart() { StopMovement();UIManager.Instance?.ShowNotification("🧬 실험체 X-0");yield return FlashEffect(new Color(0.3f,0.8f,1f),0.2f,5);yield return new WaitForSeconds(1.2f); }
    protected override IEnumerator Phase1Loop() { float tt=7f,st=4f;while(_currentPhase==1&&!_isDead){tt-=Time.deltaTime;st-=Time.deltaTime;if(st<=0){yield return Spikes(3);st=5f;}else if(tt<=0){yield return Teleport();tt=8f;}else{MoveTowardPlayer();DealDamageInRadius(1.2f,15f);}yield return null;} }
    protected override IEnumerator Phase2Loop() { float bt=6f,ct=10f;while(_currentPhase==2&&!_isDead){bt-=Time.deltaTime;ct-=Time.deltaTime;if(ct<=0){yield return Cyclone();ct=12f;}else if(bt<=0){yield return Beam();bt=6f;}else MoveTowardPlayer(1.2f);yield return null;} }
    protected override IEnumerator Phase3Loop() { SpawnCopies(2);UIManager.Instance?.ShowNotification("🔬 복제!");float mt=5f,st=3f;while(_currentPhase==3&&!_isDead){mt-=Time.deltaTime;st-=Time.deltaTime;if(mt<=0){yield return Magnetic();mt=7f;}if(st<=0){yield return Spikes(5);st=4f;}MoveTowardPlayer(1.5f);yield return null;} }

    private IEnumerator Spikes(int count) { StopMovement();for(int i=0;i<count;i++){Vector3 p=_player!=null?_player.position+new Vector3(UnityEngine.Random.Range(-2.5f,2.5f),0,UnityEngine.Random.Range(-2.5f,2.5f)):transform.position;p.y=0;if(spikePrefab!=null){var go=Instantiate(spikePrefab,p,Quaternion.identity);go.GetComponent<TimedDamageZone>()?.Init(p1SpikeDmg,1f,3f);}yield return new WaitForSeconds(0.3f);} }
    private IEnumerator Teleport() { yield return FlashEffect(new Color(0.3f,0.8f,1f),0.08f,3);if(_player!=null){float a=UnityEngine.Random.Range(0f,360f)*Mathf.Deg2Rad;Vector3 p=_player.position+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*2.5f;p.y=0;if(NavMesh.SamplePosition(p,out NavMeshHit h,3f,NavMesh.AllAreas))_agent?.Warp(h.position);else transform.position=p;}yield return FlashEffect(new Color(0.3f,0.8f,1f),0.08f,2); }
    private IEnumerator Beam() { StopMovement();UIManager.Instance?.ShowNotification("⚡");float e=0f;while(e<2.5f){e+=Time.deltaTime;DealDamageInRadius(1.8f,p2BeamDmg*Time.deltaTime);yield return null;} }
    private IEnumerator Cyclone() { StopMovement();UIManager.Instance?.ShowNotification("🌀");float e=0f,a=0f;while(e<3f){e+=Time.deltaTime;a+=360f*Time.deltaTime*2f;float r=a*Mathf.Deg2Rad;FireProjectile(beamPrefab,new Vector3(Mathf.Cos(r),0,Mathf.Sin(r)),8f,20f);yield return new WaitForSeconds(0.12f);} }
    private IEnumerator Magnetic()
    {
        StopMovement();if(magneticFX)Instantiate(magneticFX,transform.position,Quaternion.identity);UIManager.Instance?.ShowNotification("🧲");
        var rb=FindFirstObjectByType<PlayerController>()?.GetComponent<Rigidbody>();
        if(rb!=null){Vector3 d=(transform.position-rb.transform.position).normalized;d.y=0.2f;bool wk=rb.isKinematic;rb.isKinematic=false;rb.AddForce(d.normalized*p3MagForce,ForceMode.Impulse);}
        DealDamageInRadius(p3MagRadius,30f);yield return new WaitForSeconds(0.5f);
    }
    private void SpawnCopies(int count) { if(copyPrefab==null)return;for(int i=0;i<count;i++){float a=i*(360f/count)*Mathf.Deg2Rad;Vector3 p=transform.position+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*4f;if(NavMesh.SamplePosition(p,out NavMeshHit h,3f,NavMesh.AllAreas))p=h.position;Instantiate(copyPrefab,p,Quaternion.identity);} }
}
