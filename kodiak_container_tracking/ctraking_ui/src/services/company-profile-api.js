import {registrationApi} from './registration-api.js';
export function companyProfileApi(request){return {get:()=>request('/profile/company'),update:(version,body)=>request('/profile/company',{method:'PUT',version,body}),countries:()=>registrationApi.allOptions('countries')};}
