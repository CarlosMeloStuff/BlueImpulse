﻿using UnityEngine;
using System.Collections;

public class Player : MonoBehaviour {

	Transform trans;
	Rigidbody rigid;
	Vector4 glowColor = new Vector4(0.1f, 0.075f, 0.2f, 0.0f);
	public GameObject playerBullet;
	public bool canBlow = true;
	Matrix4x4 blowMatrix;
	public Material matLine;

	void Start()
	{
		trans = GetComponent<Transform>();
		rigid = GetComponent<Rigidbody>();
	}

	void Update()
	{
		TestShooter ts = TestShooter.instance;
		ts.fractions.csWorldData[0].gravity = 0.0f;
		ts.fractions.csWorldData[0].coord_scaler = new Vector3(1.0f, 1.0f, 0.9f);
		if (!canBlow) {
			ts.fractions.csWorldData[0].decelerate = 1.0f;
		}


		MeshRenderer mr = GetComponent<MeshRenderer>();
		mr.material.SetVector("_GlowColor", glowColor);

		if (Input.GetButton("Fire1"))
		{
			Shot();
		}
		if (Input.GetButtonDown("Fire2") || Input.GetButtonDown("Fire3"))
		{
			Blow();
		}
		{
			Matrix4x4 bt = Matrix4x4.identity;
			bt.SetColumn(3, new Vector4(0.0f, 0.0f, 0.5f, 1.0f));
			bt = Matrix4x4.Scale(new Vector3(5.0f, 6.0f, 10.0f)) * bt;
			blowMatrix = trans.localToWorldMatrix * bt;
		}
		{
			Vector3 move = Vector3.zero;
			move.x = Input.GetAxis("Horizontal");
			move.y = Input.GetAxis("Vertical");
			rigid.velocity = move*5.0f;
		}
		{
			Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
			Plane plane = new Plane(new Vector3(0.0f,0.0f,1.0f), Vector3.zero);
			float distance = 0;
			if (plane.Raycast(ray, out distance))
			{
				trans.rotation = Quaternion.LookRotation(ray.GetPoint(distance) - trans.position);
			}
		}
	}

	void Shot()
	{
		if (canBlow)
		{
			Instantiate(playerBullet, trans.position + trans.forward.normalized * 1.0f, trans.rotation);
		}
		else
		{
			TestShooter ts = TestShooter.instance;
			Vector3 pos = transform.position;
			Vector3 dir = transform.forward;
			CSParticle[] additional = new CSParticle[26];
			for (int i = 0; i < additional.Length; ++i)
			{
				additional[i].position = pos + dir * 0.5f;
				additional[i].velocity = (dir + new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f), 0.0f)) * 10.0f;
			}
			ts.fractions.AddParticles(additional);
		}
	}

	void Blow()
	{
		Vector3 pos = trans.position;
		float strength = 2000.0f;

		CSForce force = new CSForce();
		force.info.shape_type = CSForceShape.Box;
		force.info.dir_type = CSForceDirection.Radial;
		force.info.strength = strength;
		force.info.center = pos - (trans.forward * 6.0f);
		CSImpl.BuildBox(ref force.box, blowMatrix, Vector3.one);
		ParticleForce.AddForce(ref force);
	}

	void OnGUI()
	{
		if (canBlow)
		{
			Color blue = Color.blue;
			blue.a = 0.25f;
			Matrix4x4 mat = blowMatrix * Matrix4x4.Scale(new Vector3(0.0f, 1.0f, 1.0f));
			matLine.SetPass(0);
			DrawWireCube(mat, blue);
		}
	}
	public static void DrawWireCube(Matrix4x4 mat, Color col)
	{
		const float s = 0.5f;
		Vector4[] vertices = new Vector4[8] {
			new Vector4( s, s, s, 1.0f),
			new Vector4(-s, s, s, 1.0f),
			new Vector4( s,-s, s, 1.0f),
			new Vector4( s, s,-s, 1.0f),
			new Vector4(-s,-s, s, 1.0f),
			new Vector4( s,-s,-s, 1.0f),
			new Vector4(-s, s,-s, 1.0f),
			new Vector4(-s,-s,-s, 1.0f),
		};
		for (int i = 0; i < vertices.Length; ++i )
		{
			vertices[i] = mat * vertices[i];
		}
		int[] indices = new int[24] {
			0,1, 0,2, 0,3,
			1,4, 1,6,
			2,4, 2,5,
			3,5, 3,6,
			4,7,
			5,7,
			6,7
		};


		GL.Begin(GL.LINES);
		GL.Color(col);
		for (int i = 0; i < indices.Length; ++i )
		{
			GL.Vertex(vertices[indices[i]]);
		}
		GL.End();
	}
}
