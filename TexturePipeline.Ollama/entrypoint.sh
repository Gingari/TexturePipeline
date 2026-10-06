#!/bin/sh
set -e

ollama serve &
OLLAMA_PID=$!

until ollama list >/dev/null 2>&1
do
    sleep 2
done

if ! ollama show phi3:mini >/dev/null 2>&1
then
    ollama pull phi3:mini
fi

wait $OLLAMA_PID
