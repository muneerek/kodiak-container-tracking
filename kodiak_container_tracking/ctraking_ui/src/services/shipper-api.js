import { registrationApi } from './registration-api.js';
export function shipperApi(request) {
  return {
    list: (page = 1, pageSize = 25, search = '') => request('/shippers?page=' + page + '&pageSize=' + pageSize + (search ? '&search=' + encodeURIComponent(search) : '')),
    get: id => request('/shippers/' + id),
    create: body => request('/shippers', { method: 'POST', body }),
    update: (id, version, body) => request('/shippers/' + id, { method: 'PUT', version, body }),
    remove: (id, version) => request('/shippers/' + id, { method: 'DELETE', version }),
    countries: () => registrationApi.allOptions('countries'),
    categories: () => registrationApi.allOptions('businesscategories'),
    uploadLogo(id, version, blob) { const body = new FormData(); body.append('photo', blob, 'logo.jpg'); return request('/shippers/' + id + '/logo', { method: 'PUT', version, body }); },
    removeLogo: (id, version) => request('/shippers/' + id + '/logo', { method: 'DELETE', version })
  };
}
