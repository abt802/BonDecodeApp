# BonDecodeApp
Application for verification of [B61Decoder](https://github.com/abt802/B61Decoder)<br/>

B61Decoder.dllの動作確認用コマンドラインアプリケーションです。<br/>
VS2022でbuildを確認。<br/>

以下の引数で利用してください。

> BonDecode.exe \<Decoder.dll> \<EcryptedFile> \<OutputFile>

# BonDecodeGui
Application Wrapper for BonDecodeApp.exe

上記BonDecodeApp.exeをGUIより操作するアプリケーションです。<br/>
VS2022でbuildを確認。<br/>

以下のファイルをBonDecodeGui.extと同じフォルダに配置して利用してください。

* B61Decoder.dll (or Compatible DLL of IB25Decoder interface)
* BonDecode.App.exe

![BonDecodeGui screenshot](https://github.com/user-attachments/assets/ed6bb1f7-144f-4d76-8ab5-0780fdbe81b8)

# SmartCardChecker
Application for SCard API Check

SCard～ APIによるSmartCard Readerの正常性を確認するアプリケーションです。<br/>
VS2022でbuildを確認。<br/>

BCAS/ACAS/FelicaでのID取得までの動作検証をしています。<br/>

![SmartCarChecker screenshot](https://github.com/user-attachments/assets/2f7cc875-1608-41b0-a230-e59a7c7ee7ff)
