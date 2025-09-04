using System.Collections.Generic;
using UnityEngine;

public class FlatGen : MonoBehaviour
{

	[Header("Init Settings")]
	public int numChunks = 4;
	public int numPointsPerAxis = 10;
	public float boundsSize = 200;
	public float isoLevel = 0f;
	public bool useFlatShading;

	[Header("Noise")]
	public float noiseScale = 30;
	public float noiseHeightMultiplier = 10;
	public float planeHeight = 0;

	[Header("Post-Process")]
	public bool blurMap;
	public int blurRadius = 3;

	[Header("References")]
	public ComputeShader meshCompute;
	public ComputeShader densityCompute; // set to FlatMap.compute
	public ComputeShader blurCompute;
	public Material terrainMaterial;

	ComputeBuffer triangleBuffer;
	ComputeBuffer triCountBuffer;
	RenderTexture rawDensityTexture;
	RenderTexture processedDensityTexture;
	RenderTexture originalMap;
	FlatChunk[] chunks;
    public ComputeShader editCompute;

	VertexData[] vertexDataArray;

	void Start()
	{
		InitTextures();
		CreateBuffers();
		CreateChunks();
		GenerateAllChunks();
	}

	void InitTextures()
	{
		int size = numChunks * (numPointsPerAxis - 1) + 1;
		Create3DTexture(ref rawDensityTexture, size, "Flat Raw Density");
		if (blurMap)
		{
			Create3DTexture(ref processedDensityTexture, size, "Flat Processed Density");
		}
		else
		{
			processedDensityTexture = rawDensityTexture;
		}

		densityCompute.SetTexture(0, "DensityTexture", rawDensityTexture);
		if (blurCompute)
		{
			blurCompute.SetTexture(0, "Source", rawDensityTexture);
			blurCompute.SetTexture(0, "Result", processedDensityTexture);
		}
		meshCompute.SetTexture(0, "DensityTexture", processedDensityTexture);
	}

	void GenerateAllChunks()
	{
		ComputeDensity();
		// Create a stable copy for shading like GenTest to avoid artifacts after edits
		ComputeHelper.CreateRenderTexture3D(ref originalMap, processedDensityTexture);
		ComputeHelper.CopyRenderTexture3D(processedDensityTexture, originalMap);
		// Propagate globals needed by shader
		if (terrainMaterial)
		{
			terrainMaterial.SetTexture("DensityTex", originalMap);
			terrainMaterial.SetFloat("planetBoundsSize", boundsSize);
			terrainMaterial.SetInt("isFlatWorld", 1);
		}
		for (int i = 0; i < chunks.Length; i++)
		{
			GenerateChunk(chunks[i]);
		}
	}

	void ComputeDensity()
	{
		int textureSize = rawDensityTexture.width;
		densityCompute.SetInt("textureSize", textureSize);
		densityCompute.SetFloat("worldSize", boundsSize);
		densityCompute.SetFloat("noiseHeightMultiplier", noiseHeightMultiplier);
		densityCompute.SetFloat("noiseScale", noiseScale);
		densityCompute.SetFloat("planeHeight", planeHeight);
		ComputeHelper.Dispatch(densityCompute, textureSize, textureSize, textureSize);

		ProcessDensityMap();
	}

	void ProcessDensityMap()
	{
		if (blurMap && blurCompute)
		{
			int size = rawDensityTexture.width;
			blurCompute.SetInts("brushCentre", 0, 0, 0);
			blurCompute.SetInt("blurRadius", blurRadius);
			blurCompute.SetInt("textureSize", size);
			ComputeHelper.Dispatch(blurCompute, size, size, size);
		}
	}

	void GenerateChunk(FlatChunk chunk)
	{
		int numVoxelsPerAxis = numPointsPerAxis - 1;
		int marchKernel = 0;

		meshCompute.SetInt("textureSize", processedDensityTexture.width);
		meshCompute.SetInt("numPointsPerAxis", numPointsPerAxis);
		meshCompute.SetFloat("isoLevel", isoLevel);
		meshCompute.SetFloat("planetSize", boundsSize);
		triangleBuffer.SetCounterValue(0);
		meshCompute.SetBuffer(marchKernel, "triangles", triangleBuffer);

		Vector3 chunkCoord = (Vector3)chunk.id * (numPointsPerAxis - 1);
		meshCompute.SetVector("chunkCoord", chunkCoord);

		ComputeHelper.Dispatch(meshCompute, numVoxelsPerAxis, numVoxelsPerAxis, numVoxelsPerAxis, marchKernel);

		int[] vertexCountData = new int[1];
		triCountBuffer.SetData(vertexCountData);
		ComputeBuffer.CopyCount(triangleBuffer, triCountBuffer, 0);
		triCountBuffer.GetData(vertexCountData);
		int numVertices = vertexCountData[0] * 3;
		triangleBuffer.GetData(vertexDataArray, 0, 0, numVertices);
		chunk.CreateMesh(vertexDataArray, numVertices, useFlatShading);
	}

	void CreateBuffers()
	{
		int numVoxelsPerAxis = numPointsPerAxis - 1;
		int numVoxels = numVoxelsPerAxis * numVoxelsPerAxis * numVoxelsPerAxis;
		int maxTriangleCount = numVoxels * 5;
		int maxVertexCount = maxTriangleCount * 3;
		triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
		triangleBuffer = new ComputeBuffer(maxVertexCount, ComputeHelper.GetStride<VertexData>(), ComputeBufferType.Append);
		vertexDataArray = new VertexData[maxVertexCount];
	}

	void OnDestroy()
	{
		ComputeHelper.Release(triangleBuffer, triCountBuffer);
		foreach (FlatChunk chunk in chunks)
		{
			chunk.Release();
		}
		ComputeHelper.Release(originalMap);
	}

	void CreateChunks()
	{
		chunks = new FlatChunk[numChunks * numChunks * numChunks];
		float chunkSize = (boundsSize) / numChunks;
		int i = 0;
		for (int y = 0; y < numChunks; y++)
		{
			for (int x = 0; x < numChunks; x++)
			{
				for (int z = 0; z < numChunks; z++)
				{
					Vector3Int coord = new Vector3Int(x, y, z);
					float posX = (-(numChunks - 1f) / 2 + x) * chunkSize;
					float posY = (-(numChunks - 1f) / 2 + y) * chunkSize;
					float posZ = (-(numChunks - 1f) / 2 + z) * chunkSize;
					Vector3 centre = new Vector3(posX, posY, posZ);

					GameObject meshHolder = new GameObject($"FW Chunk ({x}, {y}, {z})");
					meshHolder.transform.parent = transform;
					int terrainLayer = LayerMask.NameToLayer("Terrain");
					meshHolder.layer = (terrainLayer != -1) ? terrainLayer : gameObject.layer;

					FlatChunk chunk = new FlatChunk(coord, centre, chunkSize, numPointsPerAxis, meshHolder);
					chunk.SetMaterial(terrainMaterial);
					chunks[i] = chunk;
					i++;
				}
			}
		}
	}

	void Create3DTexture(ref RenderTexture texture, int size, string name)
	{
		var format = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat;
		if (texture == null || !texture.IsCreated() || texture.width != size || texture.height != size || texture.volumeDepth != size || texture.graphicsFormat != format)
		{
			if (texture != null)
			{
				texture.Release();
			}
			const int numBitsInDepthBuffer = 0;
			texture = new RenderTexture(size, size, numBitsInDepthBuffer);
			texture.graphicsFormat = format;
			texture.volumeDepth = size;
			texture.enableRandomWrite = true;
			texture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
			texture.Create();
		}
		texture.wrapMode = TextureWrapMode.Repeat;
		texture.filterMode = FilterMode.Bilinear;
		texture.name = name;
	}

	public void Terraform(Vector3 point, float weight, float radius)
	{
		if (editCompute == null)
		{
			Debug.LogError("FlatGen: editCompute is not assigned. Assign EditTexture.compute to enable terraforming.");
			return;
		}
		int editTextureSize = rawDensityTexture.width;
		float editPixelWorldSize = boundsSize / editTextureSize;
		int editRadius = Mathf.CeilToInt(radius / editPixelWorldSize);

		// Map world point to texture space in generator-local coordinates
		Vector3 localPoint = point - transform.position;
		float tx = Mathf.Clamp01((localPoint.x + boundsSize / 2) / boundsSize);
		float ty = Mathf.Clamp01((localPoint.y + boundsSize / 2) / boundsSize);
		float tz = Mathf.Clamp01((localPoint.z + boundsSize / 2) / boundsSize);

		int editX = Mathf.RoundToInt(tx * (editTextureSize - 1));
		int editY = Mathf.RoundToInt(ty * (editTextureSize - 1));
		int editZ = Mathf.RoundToInt(tz * (editTextureSize - 1));

		editCompute.SetFloat("weight", weight);
		editCompute.SetFloat("deltaTime", Time.deltaTime);
		editCompute.SetInts("brushCentre", editX, editY, editZ);
		editCompute.SetInt("brushRadius", editRadius);
		editCompute.SetInt("size", editTextureSize);
		editCompute.SetTexture(0, "EditTexture", rawDensityTexture);
		ComputeHelper.Dispatch(editCompute, editTextureSize, editTextureSize, editTextureSize);

		if (blurMap && blurCompute)
		{
			blurCompute.SetInt("textureSize", editTextureSize);
			blurCompute.SetInts("brushCentre", editX - blurRadius - editRadius, editY - blurRadius - editRadius, editZ - blurRadius - editRadius);
			blurCompute.SetInt("blurRadius", blurRadius);
			blurCompute.SetInt("brushRadius", editRadius);
			int k = (editRadius + blurRadius) * 2;
			ComputeHelper.Dispatch(blurCompute, k, k, k);
		}

		float worldRadius = (editRadius + 1 + ((blurMap) ? blurRadius : 0)) * editPixelWorldSize;
		int regenCount = 0;
		for (int i = 0; i < chunks.Length; i++)
		{
			FlatChunk chunk = chunks[i];
			Vector3 chunkWorldCentre = chunk.centre + transform.position;
			if (MathUtility.SphereIntersectsBox(point, worldRadius, chunkWorldCentre, Vector3.one * chunk.size))
			{
				GenerateChunk(chunk);
				regenCount++;
			}
		}

		if (regenCount == 0)
		{
			// Fallback: regen all if bounds mapping mismatched
			for (int i = 0; i < chunks.Length; i++)
			{
				GenerateChunk(chunks[i]);
			}
		}
	}
}


