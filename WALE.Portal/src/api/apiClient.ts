import {Client} from './generated/apiClient';

export let waleApiBaseUrl = import.meta.env.WALE_API_BASE_URL;
if (!waleApiBaseUrl) waleApiBaseUrl = "http://localhost:8080";

export const waleApiClient = new Client(waleApiBaseUrl);

export default waleApiClient;