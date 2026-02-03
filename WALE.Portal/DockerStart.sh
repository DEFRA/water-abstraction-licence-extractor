#!/bin/bash

printf "window.envs = {}; window.envs.WALE_API_BASE_URL = '$WALE_API_BASE_URL';" > /usr/share/nginx/html/env_config.js
nginx -g 'daemon off;'