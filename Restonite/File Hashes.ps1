"**MD5:** {0}" -f $($(CertUtil -hashfile bin\Release\net472\Restonite.dll MD5)[1] -replace " ","")
"**SHA256:** {0}" -f $($(CertUtil -hashfile bin\Release\net472\Restonite.dll SHA256)[1] -replace " ","")
