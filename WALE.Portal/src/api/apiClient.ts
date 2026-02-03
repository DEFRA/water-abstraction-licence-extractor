import {Client} from './generated/apiClient';

// @ts-ignore
export let waleApiBaseUrl = window.envs.WALE_API_BASE_URL;

if (!waleApiBaseUrl) {
    console.log("Base URL isn't set - defaulting");
    waleApiBaseUrl = "http://localhost:8080";
}

export const waleApiClient = new Client(waleApiBaseUrl);

export default waleApiClient;