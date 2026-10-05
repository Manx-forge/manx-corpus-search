#!/bin/bash
# The Codespace's site (see devcontainer.json).
#   build: clone the data and the lemma tables, build the client and the server (once, when the Codespace is created)
#   run:   start the site on port 5000 in the background (each time the Codespace starts); log in /tmp/corpus-search.log
set -euo pipefail
cd "$(dirname "$0")/.."
DATA=${CORPUS_DATA:-/workspaces/data}

if [ "$1" = build ]; then
    git submodule update --init --depth 1 CorpusSearch/external/manx-lemma-data
    mkdir -p "$DATA"
    [ -d "$DATA/text" ] || git clone --depth 1 https://github.com/david-allison/manx-search-data.git "$DATA/text"
    [ -d "$DATA/speech" ] || git clone --depth 1 https://github.com/Manx-forge/manx-speech-corpus.git "$DATA/speech"
    (cd CorpusSearch/ClientApp && npm ci && npm run build)
    dotnet build CorpusSearch/CorpusSearch.csproj -c Release
else
    cd CorpusSearch  # the content root: the client is served from ClientApp/build
    ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://0.0.0.0:5000 \
        Loading__OpenDataPath="$DATA/text/OpenData" Speech__OpenDataPath="$DATA/speech/OpenData" \
        nohup dotnet bin/Release/net10.0/CorpusSearch.dll > /tmp/corpus-search.log 2>&1 &
    echo "Loading both corpora (a few minutes): the site opens on port 5000 when ready. Log: /tmp/corpus-search.log"
fi
