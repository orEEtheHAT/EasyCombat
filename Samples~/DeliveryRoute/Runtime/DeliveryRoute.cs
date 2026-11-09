using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace EasyCombat.Samples
{
    public sealed class DeliveryRoute:MonoBehaviour
    {
        public Rigidbody Cart,Parcel;
        public WheelContactSource[] Wheels;
        public MovementField Turret;
        public NavMeshAgent Agent;
        public NavMeshSpeedAdapter Speed;
        public Transform Road;
        public Camera View;
        public LineRenderer Outline;
        private NavMeshData nav; private NavMeshDataInstance instance;
        private bool driving,highSpeed,turned;
        private Vector3 destination;
        private void Start()
        {
            var sources=new List<NavMeshBuildSource> { new NavMeshBuildSource
                {shape=NavMeshBuildSourceShape.Box,transform=Road.localToWorldMatrix,size=Vector3.one} };
            nav=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0),sources,new Bounds(Vector3.zero,new Vector3(30,10,42)),Vector3.zero,Quaternion.identity);
            if(nav) { instance=NavMesh.AddNavMeshData(nav); Agent.enabled=true; }
            destination=new Vector3(-4,0,5); if(Agent.isOnNavMesh) Agent.SetDestination(destination);
        }
        private void OnDestroy() { if(instance.valid) instance.Remove(); if(nav) Destroy(nav); }
        private void Update()
        {
            if(Agent.enabled && Agent.isOnNavMesh && !Agent.pathPending && Agent.remainingDistance<.45f)
            { destination.x=-destination.x; Agent.SetDestination(destination); }
            if(View) { View.transform.position=Cart.position+new Vector3(9,10,-11); View.transform.LookAt(Cart.position+Vector3.forward*3); }
            if(Outline)
            {
                Outline.enabled=Turret.GetComponent<CombatAreaTrigger>().IsRunning;
                if(Outline.enabled)
                {
                    var a=Turret.SensorProfile.Area; var t=Turret.transform; Outline.positionCount=19; Outline.SetPosition(0,t.position);
                    for(int i=0;i<=16;i++) Outline.SetPosition(i+1,t.position+Quaternion.AngleAxis(Mathf.Lerp(-a.ConeAngle*.5f,a.ConeAngle*.5f,i/16f),t.up)*t.forward*a.Radius);
                    Outline.SetPosition(18,t.position);
                }
            }
        }
        private void FixedUpdate()
        {
            foreach(var w in Wheels) { var wheel=w.GetComponent<WheelCollider>(); wheel.motorTorque=driving && Cart.linearVelocity.magnitude<5 ? 16 : 0; wheel.brakeTorque=driving ? 0 : 45; }
        }
        private void OnGUI()
        {
            float scale=Mathf.Min(Screen.width/960f,Screen.height/640f); var matrix=GUI.matrix; GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            GUILayout.BeginArea(new Rect(15,15,300,590),GUI.skin.box);
            GUILayout.Label("EASYCOMBAT  /  ДОСТАВКА"); GUILayout.Label("Дорога → уступ → грязь");
            if(GUILayout.Button(driving ? "Тормозить" : "Ехать")) driving=!driving;
            if(GUILayout.Button("Сбросить посылку с высоты 3 м"))
            { Parcel.position=Cart.position+Cart.transform.right*2+Vector3.up*3; Parcel.linearVelocity=Vector3.zero; Parcel.angularVelocity=Vector3.zero; Parcel.WakeUp(); }
            if(GUILayout.Button("Вернуть посылку в кузов"))
            { Parcel.position=Cart.transform.TransformPoint(new Vector3(0,.85f,0)); Parcel.rotation=Cart.rotation; Parcel.linearVelocity=Cart.linearVelocity; Parcel.angularVelocity=Vector3.zero; Parcel.GetComponent<Health>().ResetToFull(); }
            if(GUILayout.Button("Повернуть пушку")) { turned=!turned; Turret.transform.localRotation=Quaternion.Euler(0,turned ? 100 : 0,0); }
            if(GUILayout.Button("Включить / выключить луч")) { if(Turret.GetComponent<CombatAreaTrigger>().IsRunning) Turret.Cease(); else Turret.Fire(); }
            if(GUILayout.Button("Сменить скорость ИИ: 6 / 8")) { highSpeed=!highSpeed; Speed.BaseSpeed=highSpeed ? 8 : 6; }
            GUILayout.Label($"Телега {Cart.linearVelocity.magnitude:0.0} м/с   Посылка: {Parcel.GetComponent<Health>().Current:0.0} HP");
            foreach(var w in Wheels) GUILayout.Label($"{w.name}: {w.Durability.Target.Current:0.0} HP");
            GUILayout.Label($"Враг: база {Speed.BaseSpeed:0.0} → лимит {Agent.speed:0.0} м/с");
            GUILayout.Label("Контур показывает сектор; преграды внутри могут закрывать цель.");
            GUILayout.EndArea(); GUI.matrix=matrix;
        }
    }
}
