"**MD5:** {0}" -f $($(CertUtil -hashfile bin\Release\Restonite.nupkg MD5)[1] -replace " ","")
"**SHA256:** {0}" -f $($(CertUtil -hashfile bin\Release\Restonite.nupkg SHA256)[1] -replace " ","")
