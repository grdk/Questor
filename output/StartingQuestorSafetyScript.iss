function main()
{
	#if ${Extension[isxstealth]}
    echo "isxstealth already loaded"
	#endif
	
	#if !${Extension[isxstealth]}
	echo "Loading isxstealth"
	ext isxstealth
	BlockMiniDump true
	StealthModule isxstealth.dll
	#endif
	
}