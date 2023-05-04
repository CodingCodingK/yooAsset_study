using System.Collections;
using UnityEngine;
using UniFramework.Event;
using UniFramework.Singleton;
using YooAsset;

public class Boot : MonoBehaviour
{
	/// <summary>
	/// 资源系统运行模式
	/// </summary>
	public EPlayMode PlayMode = EPlayMode.EditorSimulateMode;

	void Awake()
	{
		Debug.Log($"资源系统运行模式：{PlayMode}");
		Application.targetFrameRate = 60;
		Application.runInBackground = true;
	}
	void Start()
	{
		// // 初始化事件系统
		// UniEvent.Initalize();
		//
		// // 初始化单例系统
		// UniSingleton.Initialize();
		//
		// // 初始化资源系统
		// YooAssets.Initialize();
		// YooAssets.SetOperationSystemMaxTimeSlice(30);
		//
		// // 创建补丁管理器
		// UniSingleton.CreateSingleton<PatchManager>();
		//
		// // 开始补丁更新流程
		// PatchManager.Instance.Run(PlayMode);
		StartCoroutine(Load());
	}
	
	
	
	
	// 去掉状态机的简化加载流程，API调用简单版
	IEnumerator Load()
	{
#region 初始化  对应FsmInitialize

		yield return new WaitForSeconds(1f);
		
		// 初始化资源系统
		YooAssets.Initialize();
		// 创建默认的资源包
		var package = YooAssets.CreatePackage("DefaultPackage");
		// 设置该资源包为默认的资源包，可以使用YooAssets相关加载接口加载该资源包内容。
		YooAssets.SetDefaultPackage(package);
		
		if (PlayMode == EPlayMode.EditorSimulateMode)
		{
			var initParameters = new EditorSimulateModeParameters();
			initParameters.SimulateManifestFilePath = EditorSimulateModeHelper.SimulateBuild("DefaultPackage");
			yield return package.InitializeAsync(initParameters);
		}
		else if (PlayMode == EPlayMode.HostPlayMode)
		{
			var initParameters = new HostPlayModeParameters();
			initParameters.QueryServices = new QueryStreamingAssetsFileServices();
			initParameters.DefaultHostServer = "http://127.0.0.1/CDN/PC/yooAssetTestDemo";
			initParameters.FallbackHostServer = "http://127.0.0.1/CDN/PC/yooAssetTestDemo";
			yield return package.InitializeAsync(initParameters);
		}
		else if (PlayMode == EPlayMode.OfflinePlayMode)
		{
			var initParameters = new OfflinePlayModeParameters();
			yield return package.InitializeAsync(initParameters);
		}

#endregion

#region 获取资源版本  对应FsmUpdateVersion
		
		var operation = package.UpdatePackageVersionAsync();
		yield return operation;

		if (operation.Status != EOperationStatus.Succeed)
		{
			//更新失败
			Debug.LogError("获取资源版本失败：" + operation.Error);
			
		}

		string packageVersion = operation.PackageVersion;
		Debug.Log($"Updated package Version : {packageVersion}");

#endregion

#region 更新补丁清单  对应FsmUpdateVersion
		
		var operation2 = package.UpdatePackageManifestAsync(packageVersion);
		yield return operation2;

		if (operation2.Status != EOperationStatus.Succeed)
		{
			//更新失败
			Debug.LogError("更新补丁清单失败：" +operation.Error);
		}

		//更新成功 保存资源版本号作为下次默认启动的版本!
		operation2.SavePackageVersion();

#endregion

#region 资源包下载

		yield return Download(package);

#endregion

		#region 加载资源

		// 加载场景
		string location = "scene_home";
		var sceneMode = UnityEngine.SceneManagement.LoadSceneMode.Single;
		bool activateOnLoad = true;
		SceneOperationHandle handle = package.LoadSceneAsync(location, sceneMode, activateOnLoad);
		yield return handle;
		Debug.Log($"Scene name is {handle.SceneObject.name}");

		#endregion

	}

	IEnumerator Download(ResourcePackage package)
	{
		// 创建文件下载器
		int downloadingMaxNum = 10;
		int failedTryAgain = 3;
		int timeout = 60;
		var downloader = package.CreateResourceDownloader(downloadingMaxNum, failedTryAgain, timeout);
    
		//没有需要下载的资源
		if (downloader.TotalDownloadCount == 0)
		{        
			yield break;
		}

		//需要下载的文件总数和总大小
		int totalDownloadCount = downloader.TotalDownloadCount;
		long totalDownloadBytes = downloader.TotalDownloadBytes;    

		//注册回调方法
		downloader.OnDownloadErrorCallback = (fileName, error) => { Debug.Log($"下载失败，文件名 {fileName}，error：{error}");} ;
		downloader.OnDownloadProgressCallback = (count, downloadCount, bytes, downloadBytes) => { Debug.Log($"下载中，下载文件总数{count} 已下载文件个数{downloadCount} 下载文件总大小{bytes} 已下载文件大小{downloadBytes}");};
		downloader.OnDownloadOverCallback =  succeed => { Debug.Log("下载" + (succeed ? "成功":"失败"));};
		downloader.OnStartDownloadFileCallback = (fileName, sizeBytes) =>{ Debug.Log($"下载文件名 {fileName}，大小 {sizeBytes}");} ;

		//开启下载
		downloader.BeginDownload();
		yield return downloader;

		//检测下载结果
		if (downloader.Status != EOperationStatus.Succeed)
		{
			//下载失败
			Debug.LogError("下载失败");
			yield break;
		}
	}
	
	private class QueryStreamingAssetsFileServices : IQueryServices
	{
		public bool QueryStreamingAssets(string fileName)
		{
			// StreamingAssetsHelper.cs是太空战机里提供的一个查询脚本。
			string buildinFolderName = YooAssets.GetStreamingAssetBuildinFolderName();
			return StreamingAssetsHelper.FileExists($"{buildinFolderName}/{fileName}");
		}
	}
}