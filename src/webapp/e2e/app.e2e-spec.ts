import { WebappPage } from './app.po';

describe('webapp App', () => {
  let page: WebappPage;

  beforeEach(() => {
    page = new WebappPage();
  });

  it('should display welcome message', done => {
    page.navigateTo();
    page.getParagraphText()
      .then(msg => expect(msg).toEqual('Welcome to app!!'))
      .then(done, done.fail);
  });
});
