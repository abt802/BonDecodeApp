// BonDecodeApp.cpp : このファイルには 'main' 関数が含まれています。プログラム実行の開始と終了がそこで行われます。
//

#include <iostream>
#include <fstream>
#include <filesystem>
#include <locale>

#include <Windows.h>

#include "IB25Decoder.h"

using namespace std;

HMODULE hDecoder = nullptr;

IB25Decoder2* LoadDecoder(const std::string& decoderPath);

//for commma sepalated number
class comma_numpunct : public std::numpunct<char>
{
protected:
	virtual char do_thousands_sep() const { return ','; }
	virtual std::string do_grouping() const { return "\3"; }
};


int main(int argc, char* argv[])
{
	if (argc != 4) {
		cerr << "Usage: BonDecodeApp.exe <Decoder.dll> <encryptedfile.mmts> <decryptedfile.mmts>" << endl;
		return EXIT_FAILURE;
	}

	//IB25interface instance
	IB25Decoder2* pDecoder = nullptr;
	try {
		pDecoder = LoadDecoder(argv[1]);
	}
	catch (exception& e) {
		cout << e.what() << endl;
		return EXIT_FAILURE;
	}
	cout << "Decoder Loaded:" << argv[1] << endl;

	//in, out files
	ifstream binFile(argv[2], ios::in | ios::binary);
	if (!binFile.is_open()) {
		auto mes = format("Failed to open read file:{}", argv[2]);
		cerr << mes << endl;
		return EXIT_FAILURE;
	}

	ofstream outFile(argv[3], ios::out | ios::binary);
	if (!outFile.is_open()) {
		auto mes = format("Failed to open write file:{}", argv[3]);
		cerr << mes << endl;
		return EXIT_FAILURE;
	}

	//for commma sepalated number
	cout.imbue(locale(cout.getloc(), new comma_numpunct()));
	cout << setprecision(2) <<fixed;

	// local value initialize
	auto fileSize = filesystem::file_size(argv[2]);
	streamsize readSize = 0;
	streamsize writeSize = 0;

	BYTE* dstBuffer = nullptr;
	DWORD dstSize = 0;

	auto now = time(nullptr);
	struct tm lt;
	localtime_s(&lt, &now);
	cout << "START:" << put_time(&lt, "%H:%M:%S") << endl;

	const int BLOCK_SIZE = 1024 * 1024 * 10;
	vector<BYTE> newData(BLOCK_SIZE);

	//Main loop
	while (!binFile.eof()) {
		auto rSize = binFile.read((char*)(newData.data()), BLOCK_SIZE).gcount();
		readSize += rSize;
		auto res = pDecoder->Decode(newData.data(), (DWORD)rSize, &dstBuffer, &dstSize);
		if (res) {
			outFile.write((char*)dstBuffer, dstSize);
			writeSize += dstSize;
		}
		else {
			outFile.write((char*)newData.data(), readSize);
			writeSize += readSize;
		}
		cout << "\r" << "read:" << readSize << "/" << fileSize << " write:" << writeSize;
	}

	//Finish
	{
		auto res = pDecoder->Flush(&dstBuffer, &dstSize);
		if (res) {
			outFile.write((char*)dstBuffer, dstSize);
			writeSize += dstSize;
		}
		else {
			outFile.write((char*)newData.data(), readSize);
			writeSize += readSize;
		}
		cout << "\r" << "read:" << readSize << "/" << fileSize << " write:" << writeSize;
	}

	now = time(nullptr);
	localtime_s(&lt, &now);
	std::cout << std::endl << "END:" << put_time(&lt, "%H:%M:%S") << std::endl;

	outFile.flush();
	outFile.close();

	pDecoder->Release();

	return EXIT_SUCCESS;
}


IB25Decoder2* LoadDecoder(const std::string& decoderPath)
{
	if (hDecoder) {
		throw std::runtime_error("Decoder is already loaded");
	}

	if (decoderPath.empty()) {
		throw std::invalid_argument("Decoder path is empty");
	}

	if (!filesystem::exists(decoderPath)) {
		throw std::invalid_argument("Decoder file is not exist.");
	}

	hDecoder = LoadLibraryA(decoderPath.c_str());

	if (!hDecoder) {
		throw std::runtime_error("Could not load Decoder");
	}

	IB25Decoder2* (*CreateB25Decoder)();
	CreateB25Decoder = (IB25Decoder2 * (*)())GetProcAddress(hDecoder, "CreateB25Decoder");

	if (!CreateB25Decoder) {
		FreeLibrary(hDecoder);
		hDecoder = NULL;

		throw std::runtime_error("Could not get address CreateB25Decoder()");
	}

	auto pDecoder = CreateB25Decoder();

	if (!pDecoder) {
		FreeLibrary(hDecoder);
		hDecoder = NULL;

		throw std::runtime_error("Could not get IB25Decoder");
	}

	if (!pDecoder->Initialize()) {
		FreeLibrary(hDecoder);
		hDecoder = NULL;

		throw std::runtime_error("Could not initialize IB25Decoder");
	}

	pDecoder->DiscardNullPacket(true);
	pDecoder->DiscardScramblePacket(false);
	pDecoder->EnableEmmProcess(false);

	return pDecoder;
}
