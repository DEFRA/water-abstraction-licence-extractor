import {Client} from './generated/apiClient';

export const waleApiBaseUrl = "http://localhost:8080";
export const waleApiClient = new Client(waleApiBaseUrl);

export default waleApiClient;