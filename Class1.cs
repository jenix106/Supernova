using System.Collections;
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace Supernova
{
    public class SupernovaSpell : SpellCastCharge
    {
        Item nova;
        SupernovaComponent component;
        public override void Fire(bool active)
        {
            base.Fire(active);
            if (active)
            {
                spellCaster.SetMagicOffset(new Vector3(-0.04f, 0.2f, -0.07f));
                Catalog.GetData<ItemData>("Supernova").SpawnAsync(item =>
                {
                    nova = item;
                    nova.physicBody.useGravity = false;
                    nova.IgnoreRagdollCollision(spellCaster.mana.creature.ragdoll);
                    component = nova.gameObject.AddComponent<SupernovaComponent>();
                    component.caster = spellCaster;
                }, spellCaster.magic.position + spellCaster.magic.up, spellCaster.magic.rotation);
            }
            else if (nova != null)
            {
                if (component.instance.isPlaying)
                {
                    component.instance.Stop();
                }
                nova.Despawn();
                nova = null;
                component = null;
            }
        }
        public override void UpdateCaster()
        {
            base.UpdateCaster();
            if (spellCaster.isFiring && nova != null)
            {
                if (spellCaster.mana.currentMana > 10 && spellCaster.mana.ConsumeMana(50 * Time.deltaTime * chargeSpeed * spellCaster.fireAxis))
                {
                    nova.transform.localScale += Vector3.one * Time.deltaTime * chargeSpeed * spellCaster.fireAxis * 100;
                    nova.physicBody.mass += 10;
                }
                nova.transform.position = spellCaster.magic.position + (spellCaster.magic.up * nova.transform.localScale.x * 0.05f);
            }
        }
        public override void Throw(Vector3 velocity)
        {
            base.Throw(velocity);
            if (nova != null)
            {
                nova.physicBody.velocity = Vector3.zero;
                nova.physicBody.AddForce(velocity, ForceMode.VelocityChange);
                nova.Throw();
                nova.Despawn(60);
                component.isThrown = true;
                nova = null;
                component = null;
            }
        }
    }
    public class SupernovaComponent : MonoBehaviour
    {
        Item item;
        public Creature creature;
        public SpellCaster caster;
        public EffectInstance instance;
        public bool isThrown = false;
        Collider sphere;
        public void Start()
        {
            item = GetComponent<Item>();
            sphere = item.GetCustomReference("Sphere").GetComponent<Collider>();
            item.mainCollisionHandler.OnTriggerEnterEvent += MainCollisionHandler_OnTriggerEnterEvent;
            item.disallowDespawn = true;
            creature = caster.mana.creature;
            instance = Catalog.GetData<EffectData>("SpellFireball").Spawn(item.transform);
            instance.SetIntensity(1.0f);
            instance.Play();
        }
        public void OnTriggerStay(Collider other)
        {
            if (isThrown && other.GetComponentInParent<RagdollPart>() is RagdollPart part && part?.ragdoll?.creature != creature && part.ragdoll.totalMass < item.physicBody.mass)
            {
                if (!part.ragdoll.creature.isKilled)
                {
                    CollisionInstance collisionInstance = new CollisionInstance(new DamageStruct(DamageType.Energy, item.transform.localScale.x));
                    collisionInstance.damageStruct.hitRagdollPart = part;
                    part.ragdoll.creature.Damage(collisionInstance);
                }
                if (part.ragdoll.creature.isKilled)
                {
                    List<SpellCaster> spellCasters = new List<SpellCaster>();
                    foreach (SpellCaster spellCaster in part.ragdoll.tkHandlers)
                    {
                        spellCasters.Add(spellCaster);
                    }
                    foreach (SpellCaster spellCaster in spellCasters)
                    {
                        spellCaster.telekinesis.TryRelease();
                    }
                    spellCasters.Clear();
                    List<RagdollHand> hands = new List<RagdollHand>();
                    foreach (RagdollHand hand in part.ragdoll.handlers)
                    {
                        hands.Add(hand);
                    }
                    foreach (RagdollHand hand in hands)
                    {
                        hand.UnGrab(false);
                    }
                    hands.Clear();
                    part?.ragdoll?.creature?.Despawn(1);
                }
            }
        }

        private void MainCollisionHandler_OnTriggerEnterEvent(Collider other)
        {
            if (isThrown)
            {
                if ((other?.attachedRigidbody == null || (other?.attachedRigidbody != null && other.attachedRigidbody.isKinematic) || other?.attachedRigidbody?.mass >= item.physicBody.mass) && 
                    other?.GetComponentInParent<Creature>() != creature)
                {
                    item.mainCollisionHandler.OnTriggerEnterEvent -= MainCollisionHandler_OnTriggerEnterEvent;
                    StartCoroutine(Impact(item.transform.position, -item.physicBody.velocity, item.transform.up));
                    item.Hide(true);
                    foreach(Collider collider in item.GetComponentsInChildren<Collider>())
                    {
                        collider.gameObject.SetActive(false);
                    }
                    item.physicBody.velocity = Vector3.zero;
                    item.physicBody.rigidBody.Sleep();
                    instance.Stop();
                    item.disallowDespawn = false;
                }
                else if (other.GetComponentInParent<Item>() is Item otherItem && otherItem?.mainHandler?.creature != creature && otherItem.GetComponent<SupernovaComponent>() == null)
                {
                    otherItem.Despawn(1f);
                }
            }
        }
        private IEnumerator Impact(Vector3 contactPoint, Vector3 contactNormal, Vector3 contactNormalUpward)
        {
            EffectInstance effectInstance = Catalog.GetData<EffectData>("SupernovaShockwave").Spawn(contactPoint, Quaternion.LookRotation(-contactNormal, contactNormalUpward));
            effectInstance.SetIntensity(1f);
            effectInstance.Play();
            float radius = item.transform.localScale.x;
            foreach (Effect effect in effectInstance.effects)
            {
                effect.transform.localScale = Vector3.one * radius * 0.5f;
            }
            Collider[] sphereContacts = Physics.OverlapSphere(contactPoint, radius * 2, 218119169);
            List<Creature> creaturesPushed = new List<Creature>();
            List<Rigidbody> rigidbodiesPushed = new List<Rigidbody>();
            rigidbodiesPushed.Add(item.physicBody.rigidBody);
            creaturesPushed.Add(creature);
            float waveDistance = 0.0f;
            yield return new WaitForEndOfFrame();
            while (waveDistance < radius * 2)
            {
                waveDistance += 150f * 0.1f;
                foreach (Creature creature in Creature.allActive)
                {
                    if (!creature.isKilled && Vector3.Distance(contactPoint, creature.transform.position) < waveDistance && !creaturesPushed.Contains(creature))
                    {
                        if (!creature.isPlayer)
                            creature.ragdoll.SetState(Ragdoll.State.Destabilized);
                        CollisionInstance collision = new CollisionInstance(new DamageStruct(DamageType.Energy, (radius * 4) - Vector3.Distance(contactPoint, creature.transform.position)));
                        collision.damageStruct.hitRagdollPart = creature.ragdoll.rootPart;
                        creature.Damage(collision);
                        if (item?.lastHandler?.creature != null)
                        {
                            creature.lastInteractionTime = Time.time;
                            creature.lastInteractionCreature = item.lastHandler.creature;
                        }
                        creaturesPushed.Add(creature);
                    }
                }
                foreach (Collider collider in sphereContacts)
                {
                    Breakable breakable = collider.attachedRigidbody?.GetComponentInParent<Breakable>();
                    if (breakable != null && Vector3.Distance(contactPoint, collider.transform.position) < waveDistance)
                    {
                        if (radius * radius >= breakable.instantaneousBreakVelocityThreshold && breakable.canInstantaneouslyBreak)
                            breakable.Break();
                        for (int index = 0; index < breakable.subBrokenItems.Count; ++index)
                        {
                            Rigidbody rigidBody = breakable.subBrokenItems[index].physicBody.rigidBody;
                            if (rigidBody && !rigidbodiesPushed.Contains(rigidBody))
                            {
                                rigidBody.AddExplosionForce(radius, contactPoint, radius * 2, 0f, ForceMode.VelocityChange);
                                rigidbodiesPushed.Add(rigidBody);
                            }
                        }
                        for (int index = 0; index < breakable.subBrokenBodies.Count; ++index)
                        {
                            PhysicBody subBrokenBody = breakable.subBrokenBodies[index];
                            if (subBrokenBody && !rigidbodiesPushed.Contains(subBrokenBody.rigidBody))
                            {
                                subBrokenBody.rigidBody.AddExplosionForce(radius, contactPoint, radius * 2, 0f, ForceMode.VelocityChange);
                                rigidbodiesPushed.Add(subBrokenBody.rigidBody);
                            }
                        }
                    }
                    if (collider.attachedRigidbody != null && !collider.attachedRigidbody.isKinematic && Vector3.Distance(contactPoint, collider.transform.position) < waveDistance && collider.GetComponentInParent<SupernovaComponent>() == null)
                    {
                        if (collider.attachedRigidbody.gameObject.layer != GameManager.GetLayer(LayerName.NPC) && !rigidbodiesPushed.Contains(collider.attachedRigidbody))
                        {
                            collider.attachedRigidbody.AddExplosionForce(radius, contactPoint, radius * 2, 0.0f, ForceMode.VelocityChange);
                            rigidbodiesPushed.Add(collider.attachedRigidbody);
                        }
                    }
                }
                yield return new WaitForSeconds(0.05f);
            }
            item.Despawn();
        }
    }
}
