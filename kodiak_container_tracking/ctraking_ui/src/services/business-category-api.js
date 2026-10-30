export function businessCategoryApi(request) {
  const path = '/masters/BusinessCategories';
  return {
    list: (page = 1, pageSize = 25) => request(path + '?page=' + page + '&pageSize=' + pageSize),
    get: id => request(path + '/' + id),
    create: body => request(path, { method: 'POST', body }),
    update: (id, version, body) => request(path + '/' + id, { method: 'PUT', version, body }),
    remove: (id, version) => request(path + '/' + id, { method: 'DELETE', version })
  };
}
